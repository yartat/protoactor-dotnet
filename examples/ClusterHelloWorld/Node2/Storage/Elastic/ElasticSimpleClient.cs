using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Node2.Storage.Elastic
{
    public class ElasticSimpleClient : IDisposable
    {
        private readonly HttpClient _client;

        public ElasticSimpleClient(Uri location)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            _client = new HttpClient { BaseAddress = location };
        }

        public async Task<bool> IndexExistsAsync(string indexName)
        {
            var result = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Head, indexName))
                .ConfigureAwait(false);
            return result.IsSuccessStatusCode;
        }

        public bool IndexExists(string indexName)
        {
            return IndexExistsAsync(indexName).GetAwaiter().GetResult();
        }

        public async Task<bool> CreateIndexAsync(string indexName, IndexSettings settings, string type,  IDictionary<string, PropertySettings> properties)
        {
            var requestBody = new StringBuilder();
            requestBody.Append(
                $"{{ \"settings\": {{ \"number_of_shards\": {settings.NumberOfShards}, \"number_of_replicas\": {settings.NumberOfReplicas} }}");
            if (properties?.Any() ?? false)
            {
                requestBody.Append($", \"mappings\": {{ \"{type}\": {{ \"properties\": {{");
                var isFirst = true;
                foreach (var propertySetting in properties)
                {
                    if (isFirst)
                    {
                        isFirst = false;
                    }
                    else
                    {
                        requestBody.Append(", ");
                    }

                    requestBody.Append(
                        $" \"{propertySetting.Key}\": {{ \"type\": \"{propertySetting.Value.Type}\", \"index\": {propertySetting.Value.Index.ToString().ToLower()} }}");
                }

                requestBody.Append("}}}");
            }

            requestBody.Append("}");

            var result = await _client.PutAsync(indexName, new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json"))
                .ConfigureAwait(false);
            return result.IsSuccessStatusCode;
        }

        public bool CreateIndex(string indexName, IndexSettings settings, string type,
            IDictionary<string, PropertySettings> properties)
        {
            return CreateIndexAsync(indexName, settings, type, properties).GetAwaiter().GetResult();
        }

        public async Task<(bool Result, string Message)> UpsertDocumentAsync<TDocument>(string indexName, string type, string id, TDocument document)
            where TDocument : class
        {
            var result = await _client.PostAsJsonAsync($"{indexName}/{type}/{id}/_update", new UpdateRequest<TDocument> { Document = document, Upsert = true })
                .ConfigureAwait(false);
            if (result.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var message = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
            return (false, message);
        }

        public async Task<TDocument> GetDocumentAsync<TDocument>(string indexName, string type, string id)
            where TDocument : class
        {
            var result = await _client.GetAsync($"{indexName}/{type}/{id}/_source")
                .ConfigureAwait(false);
            if (result.IsSuccessStatusCode)
            {
                return await result.Content.ReadAsAsync<TDocument>().ConfigureAwait(false);
            }

            return null;
        }

        public async Task<bool> DeleteDocumentAsync(string indexName, string type, string id)
        {
            var result = await _client.DeleteAsync($"{indexName}/{type}/{id}")
                .ConfigureAwait(false);
            return result.IsSuccessStatusCode;
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}