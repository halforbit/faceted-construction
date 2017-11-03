using System;

namespace Halforbit.Facets.Exceptions
{
    public class ParameterResolutionException : Exception
    {
        public ParameterResolutionException(string log)
        {
            Log = log;
        }

        public string Log { get; }

        public override string Message => "Parameter could not be resolved.\r\n\r\n" + Log;
    }
}
