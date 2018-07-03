using System;

namespace Halforbit.Facets.Attributes
{
    public class UsesAttribute : FacetAttribute
    {
        readonly Type _targetType;

        public UsesAttribute(
            Type targetType,
            params string[] genericParameterNames)
        {
            _targetType = targetType;
            GenericParameterNames = genericParameterNames;
        }

        public override Type TargetType => _targetType;

        public string[] GenericParameterNames { get; }

        public override string ToString()
        {
            return $"{GetType().Namespace}." +
                $"{GetType().Name.Replace("Attribute", "")}" +
                $"({TargetType.Name})";
        }
    }
}
