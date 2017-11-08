using Halforbit.Facets.Interface;
using System;

namespace Halforbit.Facets.Implementation
{
    public class EnvironmentVariableConfigurationProvider : IConfigurationProvider
    {
        public string GetValue(string key) => Environment.GetEnvironmentVariable(key);
    }
}
