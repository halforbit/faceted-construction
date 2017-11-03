using System;

namespace Halforbit.Facets.Exceptions
{
    public class ResultResolutionException : Exception
    {
        public ResultResolutionException(string log)
        {
            Log = log;
        }

        public string Log { get; }

        public override string Message => "Result could not be resolved.\r\n\r\n" + Log;
    }
}
