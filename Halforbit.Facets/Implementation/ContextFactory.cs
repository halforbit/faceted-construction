using Halforbit.Facets.Attributes;
using Halforbit.Facets.Exceptions;
using Halforbit.Facets.Interface;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using static System.Linq.Expressions.Expression;

namespace Halforbit.Facets.Implementation
{
    public class ContextFactory : IContextFactory
    {
        public ContextFactory(
            IConfigurationProvider configurationProvider = null)
        {
            _log = t => _logger.AppendLine(t);

            _configurationProvider = configurationProvider;
        }

        readonly StringBuilder _logger = new StringBuilder();

        readonly Action<string> _log;

        readonly IConfigurationProvider _configurationProvider;

        public TInterface Create<TInterface>()
            where TInterface : class
        {
            var contextMock = new Mock<TInterface>(MockBehavior.Strict);

            var contextTypeInfo = typeof(TInterface).GetTypeInfo();

            foreach(var propertyInfo in contextTypeInfo.DeclaredProperties)
            {
                var facetAttributes = GetFacetAttributes(propertyInfo);

                var propertyType = propertyInfo.PropertyType;

                if (typeof(IContext).IsAssignableFrom(propertyType))
                {
                    MockSubContextProperty(
                        contextMock,
                        propertyInfo,
                        _configurationProvider,
                        facetAttributes);

                    continue;
                }

                MockContextProperty(
                    contextMock,
                    propertyInfo,
                    facetAttributes,
                    _configurationProvider);
            }

            return contextMock.Object;
        }

        void MockSubContextProperty<TDataContext>(
            Mock<TDataContext> dataContextMock,
            PropertyInfo propertyInfo,
            IConfigurationProvider configurationProvider,
            IEnumerable<FacetAttribute> parametricAttributes)
            where TDataContext : class
        {
            var createMethod = typeof(ContextFactory)
                .GetMethod(nameof(Create))
                .MakeGenericMethod(propertyInfo.PropertyType);

            var lazyInstancer = new Lazy<object>(() => createMethod.Invoke(
                this,
                new object[] { }));

            dataContextMock
                .Setup(BuildPropertyLambda<TDataContext>(propertyInfo))
                .Returns(() => lazyInstancer.Value);
        }

        void MockContextProperty<TDataContext>(
            Mock<TDataContext> contextMock,
            PropertyInfo propertyInfo,
            IEnumerable<FacetAttribute> parametricAttributes,
            IConfigurationProvider configurationProvider)
            where TDataContext : class
        {
            var lazyInstancer = new Lazy<object>(() => FulfillObject(
                propertyInfo.PropertyType,
                parametricAttributes,
                configurationProvider));

            contextMock
                .Setup(BuildPropertyLambda<TDataContext>(propertyInfo))
                .Returns(() => lazyInstancer.Value);
        }

        object FulfillProperty(
            PropertyInfo property,
            IEnumerable<FacetAttribute> attributes,
            IConfigurationProvider configurationProvider)
        {
            _log($"Received attributes: " + attributes.JoinString());

            var discoveredAttributes = GetFacetAttributes(property);

            _log($"Discovered attributes: " + discoveredAttributes.JoinString());

            var DeclarativeAttributes = (attributes ?? Enumerable.Empty<FacetAttribute>())
                .Concat(discoveredAttributes)
                .ToList();

            return FulfillObject(
                property.PropertyType, 
                DeclarativeAttributes,
                configurationProvider);
        }

        object FulfillObject(
            Type objectType,
            IEnumerable<FacetAttribute> facetAttributes,
            IConfigurationProvider configurationProvider)
        {
            var impliedTypes = facetAttributes
                .SelectMany(a => a.ImpliedTypes)
                .Distinct()
                .ToList();

            _log($"Implied types: " + impliedTypes.JoinString());

            var typeGroups = facetAttributes
                .GroupBy(a => a.TargetType)
                .ToList();

            _log($"Type groups: " + typeGroups.JoinString(
                toString: g => $"{g.Key} {g.Count()}"));

            var interfacesToFulfill = typeGroups
                .Where(t => t.Key.GetTypeInfo().IsInterface)
                .ToList();

            _log("Interfaces to fulfill: " + interfacesToFulfill.JoinString());

            var remainingTypesToConstruct = typeGroups
                .Where(t => !t.Key.GetTypeInfo().IsInterface)
                .ToList();

            var constructed = new List<object>();

            var allowOmitOptionals = false;

            while (remainingTypesToConstruct.Any())
            {
                _log("Remaining types to construct: " + remainingTypesToConstruct.JoinString());

                var constructedSomething = false;

                foreach (var typeToConstruct in remainingTypesToConstruct.ToList())
                {
                    var targetType = typeToConstruct.Key;

                    var arglist = typeToConstruct
                        .OfType<FacetParameterAttribute>()
                        .Select(a => GetArgument(a, configurationProvider))
                        .Where(a => a.Key != null)
                        .ToList();

                    foreach (var interfaceToFulfill in interfacesToFulfill.Where(i => targetType.Implements(i.Key)))
                    {
                        arglist.AddRange(interfaceToFulfill
                            .OfType<FacetParameterAttribute>()
                            .Select(a => GetArgument(a, configurationProvider))
                            .Where(a => a.Key != null));
                    }

                    var arguments = arglist
                        .ToDictionary(
                            kv => kv.Key,
                            kv => kv.Value);

                    if (targetType.GetTypeInfo().ContainsGenericParameters)
                    {
                        targetType = targetType.MakeGenericType(objectType.GetTypeInfo().GenericTypeArguments);
                    }

                    _log($"Try construct {targetType}");

                    _log($"arguments {arguments.JoinString()}");

                    _log($"resolved {constructed.JoinString()}");

                    _log($"omit optionals {allowOmitOptionals}");

                    var instance = TryConstruct(
                        type: targetType,
                        arguments: arguments,
                        resolvedDependencies: constructed,
                        allowOmitOptionals: allowOmitOptionals);

                    if (instance != null)
                    {
                        _log($"Constructed {targetType}");

                        if (objectType.GetTypeInfo().IsAssignableFrom(instance.GetType().GetTypeInfo()))
                        {
                            return instance;
                        }

                        constructed.Add(instance);

                        remainingTypesToConstruct.Remove(typeToConstruct);

                        constructedSomething = true;
                    }
                }

                if (constructedSomething)
                {
                    allowOmitOptionals = false;
                }
                else if (!allowOmitOptionals)
                {
                    allowOmitOptionals = true;
                }
                else
                {
                    throw new ParameterResolutionException();
                        //"Stalled out; missing something.\r\n\r\n" + _logger.ToString());
                }
            }

            throw new ResultResolutionException();
        }

        public static IEnumerable<FacetAttribute> GetFacetAttributes(PropertyInfo property)
        {
            foreach (var nestedTypeAttribute in GetNestedTypeAttributes(property.DeclaringType))
            {
                yield return nestedTypeAttribute;
            }

            foreach (var propertyAttribute in property.GetCustomAttributes(true))
            {
                var sourceAttribute = propertyAttribute as SourceAttribute;

                if (sourceAttribute != null)
                {
                    foreach (var sourceType in sourceAttribute.Types)
                    {
                        foreach (var extendedAttribute in GetNestedTypeAttributes(sourceType))
                        {
                            yield return extendedAttribute;
                        }
                    }
                }
                else if (propertyAttribute is FacetAttribute)
                {
                    yield return propertyAttribute as FacetAttribute;
                }
            }
        }

        static IEnumerable<FacetAttribute> GetNestedTypeAttributes(Type type)
        {
            if (type.DeclaringType != null)
            {
                foreach (var parentAttribute in GetNestedTypeAttributes(type.DeclaringType))
                {
                    yield return parentAttribute;
                }
            }

            foreach (var attribute in type.GetTypeInfo().GetCustomAttributes(true))
            {
                var sourceAttribute = attribute as SourceAttribute;

                if (sourceAttribute != null)
                {
                    foreach (var sourceType in sourceAttribute.Types)
                    {
                        foreach (var extendedAttribute in GetNestedTypeAttributes(sourceType))
                        {
                            yield return extendedAttribute;
                        }
                    }
                }
                else if (attribute is FacetAttribute)
                {
                    yield return attribute as FacetAttribute;
                }
            }
        }

        object TryConstruct(
            Type type,
            IReadOnlyDictionary<string, object> arguments,
            IReadOnlyList<object> resolvedDependencies,
            bool allowOmitOptionals)
        {
            var constructor = type.GetTypeInfo().DeclaredConstructors.Single();

            var parameters = constructor
                .GetParameters()
                .Select(p =>
                {
                    var isOptional = p.GetCustomAttribute<OptionalAttribute>() != null;

                    object o;

                    if (arguments.TryGetValue(p.Name, out o))
                    {
                        return new
                        {
                            ParameterInfo = p,

                            Value = o,

                            IsOptional = isOptional
                        };
                    }

                    return new
                    {
                        ParameterInfo = p,

                        Value = resolvedDependencies.FirstOrDefault(d => p.ParameterType
                            .GetTypeInfo()
                            .IsAssignableFrom(d.GetType().GetTypeInfo())),

                        IsOptional = isOptional
                    };
                })
                .ToArray();

            var missingParameters = parameters.Where(p =>
                p.Value == null &&
                (!allowOmitOptionals || !p.IsOptional));

            if (missingParameters.Any())
            {
                _log($"Cannot create {type}, missing parameters " +
                    missingParameters.JoinString(
                        toString: p => $"{p.ParameterInfo.ParameterType} {p.ParameterInfo.Name}"));

                return null;
            }

            return Activator.CreateInstance(
                type,
                parameters
                    .Select(p => p.Value)
                    .ToArray());
        }

        KeyValuePair<string, object> GetArgument(
            FacetParameterAttribute attribute,
            IConfigurationProvider configurationProvider)
        {
            var hasConfigKey = !string.IsNullOrWhiteSpace(attribute.ConfigKey);

            if(hasConfigKey && configurationProvider == null)
            {
                throw new NoConfigurationProviderException();
            }

            var value = hasConfigKey ?
                configurationProvider.GetValue(attribute.ConfigKey) :
                attribute.Value;

            return new KeyValuePair<string, object>(
                attribute.ParameterName,
                value);
        }

        static Expression<Func<TDataContext, object>> BuildPropertyLambda<TDataContext>(
            PropertyInfo propertyInfo)
        {
            var parameter = Parameter(typeof(TDataContext));

            var body = PropertyOrField(parameter, propertyInfo.Name);

            return Lambda<Func<TDataContext, object>>(body, parameter);
        }
    }

    public static class Extensions
    {
        public static bool Implements(
            this Type concreteType,
            Type interfaceType)
        {
            return concreteType
                .GetTypeInfo()
                .ImplementedInterfaces
                .Select(i => i.IsConstructedGenericType ? i.GetGenericTypeDefinition() : i)
                .Any(i => i.Equals(interfaceType));
        }

        public static string JoinString<TValue>(
            this IEnumerable<TValue> source,
            string separator = ", ",
            Func<TValue, string> toString = null)
        {
            if (source == null)
            {
                return string.Empty;
            }

            if (toString != null)
            {
                return string.Join(
                    separator,
                    source.Select(v => toString(v)));
            }
            else
            {
                return string.Join(
                    separator,
                    source.Select(v => v.ToString()));
            }
        }
    }
}
