using System;

namespace Halforbit.Facets.Interface
{
    public interface IDependencyResolver
    {
        bool TryResolve(Type type, out object instance);
    }
}
