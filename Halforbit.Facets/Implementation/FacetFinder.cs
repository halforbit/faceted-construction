using Halforbit.Facets.Attributes;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Halforbit.Facets.Implementation
{
    public static class FacetFinder
    {
        public static IEnumerable<FacetAttribute> GetFacetAttributes(
            PropertyInfo property,
            Action<string> log = default)
        {
            log?.Invoke($"Getting facet attributes for {property.Name} of {property.DeclaringType}");

            foreach (var nestedTypeAttribute in GetNestedTypeAttributes(property.DeclaringType, log))
            {
                log?.Invoke("Found nested facet " + nestedTypeAttribute);

                yield return nestedTypeAttribute;
            }

            foreach (var propertyAttribute in property.GetCustomAttributes(true))
            {
                var sourceAttribute = propertyAttribute as SourceAttribute;

                if (sourceAttribute != null)
                {
                    foreach (var sourceType in sourceAttribute.Types)
                    {
                        foreach (var extendedAttribute in GetNestedTypeAttributes(sourceType, log))
                        {
                            log?.Invoke("Found extended facet " + extendedAttribute);

                            yield return extendedAttribute;
                        }
                    }
                }
                else if (propertyAttribute is FacetAttribute)
                {
                    log?.Invoke("Found property facet " + propertyAttribute);

                    yield return propertyAttribute as FacetAttribute;
                }
            }
        }

        static IEnumerable<FacetAttribute> GetNestedTypeAttributes(
            Type type,
            Action<string> log)
        {
            log?.Invoke("Getting nested type facets for " + type);

            if (type.DeclaringType != null)
            {
                foreach (var parentAttribute in GetNestedTypeAttributes(type.DeclaringType, log))
                {
                    log?.Invoke("Found parent facet " + parentAttribute);

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
                        foreach (var extendedAttribute in GetNestedTypeAttributes(sourceType, log))
                        {
                            log?.Invoke("Found extended facet " + extendedAttribute);

                            yield return extendedAttribute;
                        }
                    }
                }
                else if (attribute is FacetAttribute)
                {
                    log?.Invoke("Found facet " + attribute);

                    yield return attribute as FacetAttribute;
                }
            }
        }
    }
}
