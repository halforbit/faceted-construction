using Autofac;
using Autofac.Core;
using Halforbit.Facets.Interface;
using System;

namespace Halforbit.Facets.Autofac.Implementation
{
    public class AutofacDependencyResolver : IDependencyResolver
    {
        readonly IComponentContext _componentContext;

        public AutofacDependencyResolver(
            IComponentContext componentContext)
        {
            _componentContext = componentContext;
        }

        public bool TryResolve(
            Type serviceType,
            out object instance)
        {
            if (_componentContext.TryResolve(serviceType, out instance))
            {
                return true;
            }

            // Try resolving registrationless.

            var scope = _componentContext.Resolve<ILifetimeScope>();

            using (var innerScope = scope.BeginLifetimeScope(b => b.RegisterType(serviceType)))
            {
                var componentRegistration = default(IComponentRegistration);

                if (innerScope.ComponentRegistry.TryGetRegistration(
                    new TypedService(serviceType),
                    out componentRegistration))
                {
                    try
                    {
                        instance = _componentContext.ResolveComponent(
                            componentRegistration,
                            new Parameter[0]);

                        return true;
                    }
                    catch (DependencyResolutionException)
                    {
                    }
                }
            }

            instance = null;

            return false;
        }
    }
}
