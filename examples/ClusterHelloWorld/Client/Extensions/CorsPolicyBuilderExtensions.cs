using Client.Configure;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace Client.Extensions
{
    /// <summary>
    /// CORS builder extensions
    /// </summary>
    public static class CorsPolicyBuilderExtensions
    {
        /// <summary>
        /// Configures the CORS with specified options.
        /// </summary>
        /// <param name="builder">The CORS builder.</param>
        /// <param name="options">The CORS options.</param>
        /// <returns></returns>
        public static CorsPolicyBuilder Configure(this CorsPolicyBuilder builder, IOptions<CorsConfigureOptions> options)
        {
            return builder
                .WithOrigins(string.IsNullOrEmpty(options?.Value?.Origins) ? new[] { "*" } : options.Value.Origins.Split(','))
                .WithHeaders(string.IsNullOrEmpty(options?.Value?.Headers) ? new[] { "*" } : options.Value.Headers.Split(','))
                .WithMethods(string.IsNullOrEmpty(options?.Value?.Methods) ? new[] { "*" } : options.Value.Methods.Split(','));
        }
    }
}