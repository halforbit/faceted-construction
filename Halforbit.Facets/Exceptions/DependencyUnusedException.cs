using System;

namespace Halforbit.Facets.Exceptions
{
    public class DependencyUnusedException : Exception
    {
        public DependencyUnusedException(string log)
        {
            Log = log;
        }

        public string Log { get; }

        public override string Message => "A dependency was specified with a Using attribute but never used.\r\n\r\n" + Log;
    }
}
