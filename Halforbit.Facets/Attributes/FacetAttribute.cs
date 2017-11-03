using System;

namespace Halforbit.Facets.Attributes
{
    public abstract class FacetAttribute : Attribute
    {
        public abstract Type TargetType { get; }

        public virtual Type[] ImpliedTypes => new Type[0];

        public override string ToString() => GetType().Name.Replace("Attribute", "");
    }
}
