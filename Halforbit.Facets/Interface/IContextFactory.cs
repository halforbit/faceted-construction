
namespace Halforbit.Facets.Interface
{
    public interface IContextFactory
    {
        TInterface Create<TInterface>() 
            where TInterface : class;
    }
}
