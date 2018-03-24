using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Client.Logging
{
    /// <summary>
    /// Extensions for logging
    /// </summary>
    public static class LoggingExtensions
    {
        /// <summary>
        /// Add Core logger to .NET standard logger factory
        /// </summary>
        /// <param name="loggerFactory"></param>
        /// <param name="environmentName"></param>
        /// <param name="loggerConfig"></param>
        /// <returns></returns>
        public static ILoggerFactory AddCoreLogger(
            this ILoggerFactory loggerFactory,
            string environmentName,
            LoggerOptions loggerConfig)
        {
            loggerFactory?.AddProvider(new CoreLoggerProvider(environmentName, loggerConfig ));
            return loggerFactory;
        }

        /// <summary>
        /// To the filter settings.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        public static IFilterLoggerSettings ToFilterSettings(this Dictionary<string, LogLevel> options)
        {
            var loggerSettings = new FilterLoggerSettings();
            if (options == null)
                return loggerSettings;

            foreach (var logLevel in options)
            {
                loggerSettings.Add(logLevel.Key, logLevel.Value);
            }
            return loggerSettings;
        }
    }
}