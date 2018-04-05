using System;
using System.IO;
using Client.Configure;
using Client.Extensions;
using Client.Logging;
using Client.Proto;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.PlatformAbstractions;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Swagger;

namespace Client
{
    /// <summary>
    /// Startup application
    /// </summary>
    public class Startup
    {
        private const string BetLabLoggingSectionName = "BetLab.Logging";
        private const string CorsConfigurationSectionName = "CORS";

        /// <summary>
        /// Initializes a new instance of the <see cref="Startup"/> class with host environment.
        /// </summary>
        /// <param name="env">The host environment.</param>
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        /// <summary>
        /// Gets the application configuration.
        /// </summary>
        /// <value>
        /// The configuration.
        /// </value>
        public IConfigurationRoot Configuration { get; }

        /// <summary>
        /// Configures the services to add services to the container.
        /// </summary>
        /// <param name="services">The services.</param>
        [UsedImplicitly]
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc().AddJsonOptions(options =>
            {
                options.SerializerSettings.NullValueHandling=NullValueHandling.Ignore;
            });
            services
                .Configure<CorsConfigureOptions>(Configuration.GetSection(CorsConfigurationSectionName))
                .Configure<LoggerOptions>(Configuration.GetSection(BetLabLoggingSectionName))
                .AddCoreFilters()
                .AddSingleton<ICluster>(new ProtoCluster("MyCluster", 12001, new Uri("http://127.0.0.1:8500/")))
                .AddSwaggerGen(options =>
                    {
                        options.SwaggerDoc(
                            "v1",
                            new Info
                                {
                                    Version = "v1",
                                    Title = "Deposit test API v1",
                                    Description = "Deposit test API v1"
                            });

                        options.IncludeXmlComments(Path.Combine(PlatformServices.Default.Application.ApplicationBasePath, "Client.xml"));
                    });
        }

        /// <summary>
        /// Configures the specified application runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        /// <param name="app">The application.</param>
        /// <param name="env">The host environment.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="applicationLifetime">The application lifetime.</param>
        /// <param name="loggerOptions"></param>
        /// <param name="corsOptions">The CORS options.</param>
        /// <param name="cluster"></param>
        /// <param name="server">The ASP.NET server.</param>
        [UsedImplicitly]
        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            IApplicationLifetime applicationLifetime,
            IOptions<LoggerOptions> loggerOptions,
            IOptions<CorsConfigureOptions> corsOptions,
            ICluster cluster,
            IServer server)
        {
            var loggerConfig = loggerOptions?.Value;
            loggerFactory
                .WithFilter(loggerConfig.LogLevels.ToFilterSettings())
                .AddCoreLogger(env.EnvironmentName, loggerConfig);

            app
                .UseCors(builder => builder.Configure(corsOptions))
                .UseMvc()
                .UseSwagger()
                .UseSwaggerUI(options =>
                    {
                        // fix validation error
                        // see http://stackoverflow.com/questions/32188386/cant-read-from-file-issue-in-swagger-ui
                        options.EnableValidator(null);
                        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Currency service API v1");
                    });

            applicationLifetime.ApplicationStopping.Register(cluster.Dispose);
        }
    }
}
