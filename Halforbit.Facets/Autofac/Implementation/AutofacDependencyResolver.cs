using Autofac;
using Halforbit.Facets.Interface;
using System;

namespace Halforbit.Facets.Autofac.Implementation
{
    public class AutofacDependencyResolver : IDependencyResolver
    {
        readonly IContainer _container;

        public AutofacDependencyResolver(IContainer container)
        {
            _container = container;
        }

        public bool TryResolve(Type type, out object instance)
        {
            return _container.TryResolve(type, out instance);
        }
    }
}
