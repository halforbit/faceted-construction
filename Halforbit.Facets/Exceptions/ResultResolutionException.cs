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
    }
}
