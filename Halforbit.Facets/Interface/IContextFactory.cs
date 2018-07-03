using Halforbit.Facets.Attributes;
using System.Collections.Generic;

namespace Halforbit.Facets.Interface
{
    public interface IContextFactory
    {
        TInterface Create<TInterface>(
            IReadOnlyList<FacetAttribute> facetAttributes = default) 
            where TInterface : class;
    }
}
