using Halforbit.Facets.Attributes;
using Halforbit.Facets.Implementation;
using Halforbit.Facets.Interface;
using System;
using Xunit;

namespace Halforbit.Facets.Tests
{
    public class Simple
    {
        [Fact, Trait("Type", "Unit")]
        public void SimpleTest()
        {
            var factory = new ContextFactory();

            var dataContext = factory.Create<IApplicationDataContext>();

            var dataStore = dataContext.DataStore as DataStore<string>;

            Assert.NotNull(dataStore);

            Assert.Equal("connection-string", dataStore.ConnectionString);
        }

        // API interface 

        public interface IDataStore<TData>
        {
            void Put(TData data);
        }

        // API implementation

        class DataStore<TData> : IDataStore<TData>
        {
            public DataStore(
                string connectionString)
            {
                ConnectionString = connectionString;
            }

            public string ConnectionString { get; }

            public void Put(TData data)
            {
                throw new NotImplementedException();
            }
        }

        // API facet attributes

        public class ConnectionStringAttribute : FacetParameterAttribute
        {
            public ConnectionStringAttribute(
                string value = null,
                string configKey = null) : base(value, configKey)
            {
            }

            public override Type TargetType => typeof(DataStore<>);

            public override string ParameterName => "connectionString";
        }

        // Integration

        public interface IApplicationDataContext : IContext
        {
            [ConnectionString("connection-string")]
            IDataStore<string> DataStore { get; }
        }
    }
}