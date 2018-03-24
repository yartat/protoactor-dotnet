namespace Node2.Storage
{
    /// <summary>
    /// Describes configuration properties for Elasticsearch
    /// </summary>
    public class ElasticOptions
    {
        /// <summary>
        /// Gets or sets Elasticsearch location
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the index name
        /// </summary>
        public string IndexNamePrefix { get; set; }

        /// <summary>
        /// Gets or sets the number of shards
        /// </summary>
        public int NumberOfShards { get; set; }

        /// <summary>
        /// Gets or sets the number of replicas
        /// </summary>
        public int NumberOfReplicas { get; set; }
    }
}