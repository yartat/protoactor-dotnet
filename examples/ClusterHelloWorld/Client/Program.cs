using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Client
{
    /// <summary>
    /// Entry point class
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Entry point with arguments
        /// </summary>
        /// <param name="args">The application arguments.</param>
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        internal static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseConfiguration(new ConfigurationBuilder().AddCommandLine(args).Build())
                .UseStartup<Startup>()
                .Build();
    }
}
