namespace Client.Configure
{
    /// <summary>
    /// Configuration options for CORS
    /// </summary>
    public class CorsConfigureOptions
    {
        /// <summary>
        /// Gets or sets the CORS origins.
        /// </summary>
        /// <value>
        /// The CORS origins.
        /// </value>
        public string Origins { get; set; }

        /// <summary>
        /// Gets or sets the CORS headers.
        /// </summary>
        /// <value>
        /// The CORS headers.
        /// </value>
        public string Headers { get; set; }

        /// <summary>
        /// Gets or sets the CORS methods.
        /// </summary>
        /// <value>
        /// The CORS methods.
        /// </value>
        public string Methods { get; set; }
    }
}