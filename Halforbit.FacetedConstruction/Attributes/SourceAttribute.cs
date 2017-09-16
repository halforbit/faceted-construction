using System;
using System.Collections.Generic;

namespace Halforbit.FacetedConstruction.Attributes
{
    public class SourceAttribute : Attribute
    {
        public SourceAttribute(params Type[] types)
        {
            Types = types;
        }

        public IReadOnlyList<Type> Types { get; }
    }
}
