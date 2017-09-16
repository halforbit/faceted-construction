using System;

namespace Halforbit.FacetedConstruction.Attributes
{
    public abstract class FacetAttribute : Attribute
    {
        public abstract Type TargetType { get; }

        public virtual Type[] ImpliedTypes => new Type[0];
    }
}
