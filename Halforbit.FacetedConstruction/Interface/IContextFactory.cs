
namespace Halforbit.FacetedConstruction.Interface
{
    public interface IContextFactory
    {
        TInterface Create<TInterface>() 
            where TInterface : class;
    }
}
