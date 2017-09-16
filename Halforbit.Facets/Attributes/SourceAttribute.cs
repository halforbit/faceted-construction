using System;
using System.Collections.Generic;

namespace Halforbit.Facets.Attributes
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
