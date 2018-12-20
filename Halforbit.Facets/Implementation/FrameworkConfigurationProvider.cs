using Microsoft.Extensions.Configuration;

namespace Halforbit.Facets.Implementation
{
    class FrameworkConfigurationProvider : Halforbit.Facets.Interface.IConfigurationProvider
    {
        IConfiguration _configuration;

        public FrameworkConfigurationProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GetValue(string key)
        {
            return _configuration[key];
        }
    }
}
