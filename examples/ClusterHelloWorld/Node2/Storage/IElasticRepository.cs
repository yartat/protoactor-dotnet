using System.Threading.Tasks;

namespace Node2.Storage
{
    /// <summary>
    /// Describes general repository interface for elastic
    /// </summary>
    public interface IElasticRepository
    {
        /// <summary>
        /// Gets document data by id asynchronous
        /// </summary>
        /// <param name="id">The document key</param>
        /// <returns></returns>
        Task<StorageDataItem> GetDocumentAsync(string id);

        /// <summary>
        /// Update document data 
        /// </summary>
        /// <param name="id">The document key</param>
        /// <param name="data">The document instance</param>
        /// <returns></returns>
        Task UpsertDocumentAsync(string id, StorageDataItem data);

        /// <summary>
        /// Delete document data by id asynchronous
        /// </summary>
        /// <param name="id">The document key</param>
        /// <returns></returns>
        Task DeleteAsync(string id);
    }
}