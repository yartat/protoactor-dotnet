using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Node2.Storage.Elastic;

namespace Node2.Storage
{
    public class ElasticRepository : IElasticRepository
    {
        private readonly ElasticSimpleClient _elastic;
        private readonly ElasticOptions _indexSettings;
        private readonly string _dataTypeName;

        protected string IndexName => $"{_indexSettings?.IndexNamePrefix}_{_dataTypeName}".ToLower();


        public ElasticRepository(ElasticSimpleClient elastic, ElasticOptions config, string dataTypeName)
        {
            _elastic = elastic;
            _dataTypeName = dataTypeName;
            _indexSettings = config ?? throw new ArgumentNullException(nameof(config), $"Cannot configure index for '{_dataTypeName}'");

            if (!_elastic.IndexExists(IndexName))
            {
                CreateCorrectIndex();
            }
        }

        private void CreateCorrectIndex()
        {
            var result = _elastic.CreateIndex(
                IndexName,
                new IndexSettings
                {
                    NumberOfReplicas = _indexSettings.NumberOfReplicas,
                    NumberOfShards = _indexSettings.NumberOfShards
                },
                _dataTypeName,
                new Dictionary<string, PropertySettings>
                {
                    {"value", new PropertySettings {Index = false, Type = "text"}}
                });

            if (!result)
            {
                throw new InvalidOperationException($"Can not create index: '{IndexName}'");
            }
        }

        public Task<StorageDataItem> GetDocumentAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return _elastic.GetDocumentAsync<StorageDataItem>(IndexName, _dataTypeName, id);
        }

        public async Task UpsertDocumentAsync(string id, StorageDataItem data)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            var result = await _elastic.UpsertDocumentAsync(IndexName, _dataTypeName, id, data).ConfigureAwait(false);

            if (!result.Result)
            {
                throw new InvalidOperationException($"Can not insert or update document data Id={id}.Returns:\n{result.Message}");
            }
        }

        public async Task DeleteAsync(string id)
        {
            var result = await _elastic.DeleteDocumentAsync(IndexName, _dataTypeName, id).ConfigureAwait(false);

            if (!result)
            {
                throw new InvalidOperationException($"Error occurred while deleting document with id {id}");
            }
        }
    }
}