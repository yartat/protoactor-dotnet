using System;

namespace Proto.Http
{
    public static class Extensions
    {
        public static IRootContext GetRootContext()
        {
            return new RootContext();
        }

        public static (IRootContext context, PID pid) Resolve(string id)
        {
            return (new RootContext(), new PID("nonhost", id));
        }
    }
}