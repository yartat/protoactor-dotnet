using Client.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Client.Extensions
{
    /// <summary>
    /// Extensions for add service filters to container
    /// </summary>
    public static class FiltersExtensions
    {
        /// <summary>
        /// Adds the core service filters to container.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <returns></returns>
        public static IServiceCollection AddCoreFilters(this IServiceCollection services)
        {
            return services
                .AddScoped<ValidateModelStateAttribute>()
                .AddScoped<ProcessingExceptionFilter>();
        }
    }
}