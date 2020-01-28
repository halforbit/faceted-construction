using ImpromptuInterface;
using System;
using System.Collections.Concurrent;
using System.Dynamic;

namespace Halforbit.Facets.Implementation
{
    public static class LazyCache<TInterface>
        where TInterface : class
    {
        public static TInterface Create<TImplementation>(Func<TImplementation> factory)
            where TImplementation : TInterface
        {
            return Impromptu.ActLike<TInterface>(new DynamicWrapper(factory()));
        }

        class DynamicWrapper : DynamicObject
        {
            readonly object _source;

            readonly ConcurrentDictionary<string, object> _cache = new ConcurrentDictionary<string, object>();

            public DynamicWrapper(object source)
            {
                _source = source;
            }

            public override bool TryGetMember(
                GetMemberBinder binder,
                out object result)
            {
                result = _cache.GetOrAdd(
                    binder.Name,
                    name => _source.GetType().GetProperty(name).GetValue(_source));

                return true;
            }
        }
    }
}
