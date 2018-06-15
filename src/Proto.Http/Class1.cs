using System;

namespace Proto.Http
{
    public static class Extensions
    {
        public static IRootContext GetRootContext()
        {
            return new RootContext();
        }
    }
}