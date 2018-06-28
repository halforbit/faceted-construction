using System;

namespace Halforbit.Facets.Attributes
{
    public class UsesAttribute : FacetAttribute
    {
        readonly Type _targetType;

        public UsesAttribute(
            Type targetType)
        {
            _targetType = targetType;
        }

        public override Type TargetType => _targetType;

        public override string ToString()
        {
            return $"{GetType().Namespace}." +
                $"{GetType().Name.Replace("Attribute", "")}" +
                $"({TargetType.Name})";
        }
    }
}
