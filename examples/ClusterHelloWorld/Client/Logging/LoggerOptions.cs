using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Client.Logging
{
    /// <summary>
    /// Defines logstash configuration contract
    /// </summary>
    public class LoggerOptions
    {
        /// <summary>
        /// Gets or sets the index of the logstash.
        /// </summary>
        /// <value>
        /// The index of the logstash.
        /// </value>
        public string LogstashIndex { get; set; }

        /// <summary>
        /// Gets or sets the elastic address.
        /// </summary>
        /// <value>
        /// The elastic address.
        /// </value>
        public string ElasticAddress { get; set; }

        /// <summary>
        /// Gets or sets the log levels.
        /// </summary>
        /// <value>
        /// The log levels.
        /// </value>
        public Dictionary<string, LogLevel> LogLevels { get; set; }
    }
}