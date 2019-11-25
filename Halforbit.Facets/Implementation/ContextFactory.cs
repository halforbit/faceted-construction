using Halforbit.Facets.Attributes;
using Halforbit.Facets.Exceptions;
using Halforbit.Facets.Interface;
using Halforbit.ObjectTools.Collections;
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
        readonly StringBuilder _logger = new StringBuilder();

        readonly Action<string> _log;

        readonly IConfigurationProvider _configurationProvider;

        readonly IDependencyResolver _dependencyResolver;

        public ContextFactory(
            IConfigurationProvider configurationProvider = null,
            IDependencyResolver dependencyResolver = null)
        {
            _log = t => _logger.AppendLine(t);

            _configurationProvider = configurationProvider;

            _dependencyResolver = dependencyResolver;

            var contextTypeInfo = typeof(ContextFactory).GetTypeInfo();
        }
        
        public TInterface Create<TInterface>(
            IReadOnlyList<FacetAttribute> facetAttributes = default)
            where TInterface : class
        {
            var contextMock = new Mock<TInterface>(MockBehavior.Strict);

            var contextTypeInfo = typeof(TInterface).GetTypeInfo();

            foreach(var propertyInfo in contextTypeInfo.DeclaredProperties)
            {
                var propertyFacetAttributes = (facetAttributes ?? EmptyReadOnlyList<FacetAttribute>.Instance)
                    .Concat(FacetFinder.GetFacetAttributes(propertyInfo, _log))
                    .ToList();

                var propertyType = propertyInfo.PropertyType;

                if (typeof(IContext).IsAssignableFrom(propertyType))
                {
                    MockSubContextProperty(
                        contextMock,
                        propertyInfo,
                        _configurationProvider,
                        propertyFacetAttributes);
                }
                else
                {
                    MockContextProperty(
                        contextMock,
                        propertyInfo,
                        propertyFacetAttributes,
                        _configurationProvider);
                }
            }

            return contextMock.Object;
        }

        void MockSubContextProperty<TDataContext>(
            Mock<TDataContext> dataContextMock,
            PropertyInfo propertyInfo,
            IConfigurationProvider configurationProvider,
            IReadOnlyList<FacetAttribute> parametricAttributes)
            where TDataContext : class
        {
            var createMethod = typeof(ContextFactory)
                .GetMethod(nameof(Create))
                .MakeGenericMethod(propertyInfo.PropertyType);

            var lazyInstancer = new Lazy<object>(() => createMethod.Invoke(
                this,
                new object[] { parametricAttributes }));

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
            var lazyInstancer = new Lazy<object>(() =>
            {
                _log($"Fulfilling property " + propertyInfo.Name);

                return FulfillObject(
                    propertyInfo.PropertyType,
                    typeof(TDataContext),
                    parametricAttributes,
                    configurationProvider);
            });

            contextMock
                .Setup(BuildPropertyLambda<TDataContext>(propertyInfo))
                .Returns(() => lazyInstancer.Value);
        }
        
        object FulfillObject(
            Type objectType,
            Type contextType,
            IEnumerable<FacetAttribute> facetAttributes,
            IConfigurationProvider configurationProvider)
        {
            _log($"Facet attributes: " + facetAttributes.Select(a => a.GetType().Name).JoinString());

            var constructed = new List<object>();

            var remainingDependenciesToUse = new List<object>();

            foreach (var uses in facetAttributes.OfType<UsesAttribute>())
            {
                var targetType = uses.TargetType;

                if(contextType.IsGenericType && 
                    targetType.IsGenericTypeDefinition &&
                    uses.GenericParameterNames.Any())
                {
                    var genericArgumentsByName = contextType
                        .GetGenericTypeDefinition()
                        .GetGenericArguments()
                        .Select((a, i) => new { a.Name, Type = contextType.GetGenericArguments()[i] })
                        .ToDictionary(a => a.Name, a => a.Type);

                    targetType = targetType.MakeGenericType(uses.GenericParameterNames
                        .Select(n => genericArgumentsByName[n])
                        .ToArray());
                }

                var o = default(object);

                if (!(_dependencyResolver?.TryResolve(targetType, out o) ?? false))
                {
                    var localFacets = facetAttributes
                        .Except(new[] { uses })
                        .Where(f => f.TargetType.Equals(targetType.IsGenericType ? 
                            targetType.GetGenericTypeDefinition() : 
                            targetType))
                        .ToList();

                    o = FulfillObject(
                        targetType,
                        contextType,
                        localFacets,
                        configurationProvider);

                    if(o != null)
                    {
                        facetAttributes = facetAttributes.Except(localFacets).ToList();

                        constructed.Add(o);
                    }
                    else
                    {
                        throw new ParameterResolutionException(
                            $"Dependency of type {uses.TargetType} could not be resolved.");
                    }
                }
                else
                {
                    constructed.Add(o);

                    remainingDependenciesToUse.Add(o);
                }
            }

            facetAttributes = facetAttributes.Where(f => !f.GetType().Equals(typeof(UsesAttribute)));

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
                .Where(t => 
                    !t.Key.GetTypeInfo().IsInterface && 
                    !constructed.Any(c => 
                        c.GetType().IsGenericType && 
                        c.GetType().GetGenericTypeDefinition().Equals(t.Key)))
                .ToList();

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
                        .GroupBy(kv => kv.Key)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Last().Value);

                    if (targetType.GetTypeInfo().ContainsGenericParameters)
                    {
                        targetType = targetType.MakeGenericType(objectType.GetTypeInfo().GenericTypeArguments);
                    }

                    _log($"Try construct {targetType}");

                    _log($"resolved {constructed.JoinString()}");

                    _log($"omit optionals {allowOmitOptionals}");

                    var (instance, usedDependencies) = TryConstruct(
                        type: targetType,
                        arguments: arguments,
                        resolvedDependencies: constructed,
                        allowOmitOptionals: allowOmitOptionals);

                    if (instance != null)
                    {
                        _log($"Constructed {targetType}");

                        foreach (var usedDependency in usedDependencies)
                        {
                            if(remainingDependenciesToUse.Contains(usedDependency))
                            {
                                remainingDependenciesToUse.Remove(usedDependency);
                            }
                        }

                        var implements = instance
                            .GetType()
                            .GetInterfaces()
                            .Any(i => i.IsAssignableFrom(objectType));

                        if (implements)
                            //objectType.GetTypeInfo().IsAssignableFrom(instance.GetType().GetTypeInfo()))
                        {
                            _log($"{objectType.GetTypeInfo()} -> {instance.GetType().GetTypeInfo()}");

                            if(remainingDependenciesToUse.Any())
                            {
                                throw new DependencyUnusedException(_logger.ToString());
                            }

                            return instance;
                        }
                        else
                        {
                            _log($"{objectType.GetTypeInfo()} !> {instance.GetType().GetTypeInfo()}");
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
                    throw new ParameterResolutionException(_logger.ToString());
                        //"Stalled out; missing something.\r\n\r\n" + _logger.ToString());
                }
            }

            throw new ResultResolutionException(_logger.ToString());
        }

        (object instance, IReadOnlyList<object> usedDependencies) TryConstruct(
            Type type,
            IReadOnlyDictionary<string, object> arguments,
            IReadOnlyList<object> resolvedDependencies,
            bool allowOmitOptionals)
        {
            var constructor = type.GetTypeInfo().DeclaredConstructors.Single(c => !c.IsStatic);

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
                .ToList();

            if (_dependencyResolver != null)
            {
                var unresolvedParameters = parameters
                    .Where(p => p.Value == null)
                    .ToList();

                if (unresolvedParameters.Any())
                {
                    foreach (var unresolvedParameter in unresolvedParameters)
                    {
                        var parameterType = unresolvedParameter.ParameterInfo.ParameterType;

                        if (parameterType.IsValueType) continue;

                        var instance = default(object);

                        if (_dependencyResolver.TryResolve(
                            parameterType,
                            out instance))
                        {
                            parameters[parameters.IndexOf(unresolvedParameter)] = new
                            {
                                unresolvedParameter.ParameterInfo,

                                Value = instance,

                                unresolvedParameter.IsOptional
                            };
                        }
                    }
                }
            }

            var missingParameters = parameters.Where(p =>
                p.Value == null &&
                (!allowOmitOptionals || !p.IsOptional));

            if (missingParameters.Any())
            {
                _log($"Cannot create {type}, missing parameters " +
                    missingParameters.JoinString(
                        toString: p => $"{p.ParameterInfo.ParameterType} {p.ParameterInfo.Name}"));

                return (null, EmptyReadOnlyList<object>.Instance);
            }

            var usedDependencies = parameters
                .Select(p => p.Value)
                .Where(o => resolvedDependencies.Contains(o))
                .ToList();

            var createdInstance = Activator.CreateInstance(
                type,
                parameters
                    .Select(p => p.Value)
                    .ToArray());

            return (createdInstance, usedDependencies);
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
