﻿#if NET46
using Raven.Abstractions.Data;
using Raven.Abstractions.Extensions;
using Raven.Client;
using Raven.Client.Connection;
using Raven.Client.Indexes;
#endif
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
#if !NET46
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Queries;
#endif

namespace Proto.Persistence.RavenDB
{
    public class RavenDBProvider : IProvider
    {
        private readonly IDocumentStore _store;

        public RavenDBProvider(IDocumentStore store)
        {
            _store = store;

            SetupIndexes();
        }

        private async void SetupIndexes()
        {
#if NET46
            await IndexCreation.CreateIndexesAsync(typeof(DeleteEventIndex).Assembly(), _store);
            await IndexCreation.CreateIndexesAsync(typeof(DeleteSnapshotIndex).Assembly(), _store);
#else
#if NETSTANDARD1_5
            await IndexCreation.CreateIndexesAsync(typeof(DeleteEventIndex).GetTypeInfo().Assembly, _store);
            await IndexCreation.CreateIndexesAsync(typeof(DeleteSnapshotIndex).GetTypeInfo().Assembly, _store);
#else
            await IndexCreation.CreateIndexesAsync(typeof(DeleteEventIndex).Assembly, _store);
            await IndexCreation.CreateIndexesAsync(typeof(DeleteSnapshotIndex).Assembly, _store);
#endif
#endif
        }

        public async Task<long> GetEventsAsync(string actorName, long indexStart, long indexEnd, Action<object> callback)
        {
            using (var session = _store.OpenAsyncSession())
            {
                var events = await session.Query<Event>()
                    .Where(x => x.ActorName == actorName)
                    .Where(x => x.Index >= indexStart && x.Index <= indexEnd)
                    .OrderBy(x => x.Index)
                    .ToListAsync();

                foreach (var @event in events)
                {
                    callback(@event.Data);
                }
                
                return events.Any() ? events.LastOrDefault().Index : -1;
            }
        }

        public async Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
        {
            using (var session = _store.OpenAsyncSession())
            {
                var snapshot = await session.Query<Snapshot>()
                    .Where(x => x.ActorName == actorName)
                    .OrderByDescending(x => x.Index)
                    .FirstOrDefaultAsync();

                return snapshot != null ? (snapshot.Data, snapshot.Index) : (null, 0);
            }
        }

        public async Task<long> PersistEventAsync(string actorName, long index, object @event)
        {
            using (var session = _store.OpenAsyncSession())
            {
                await session.StoreAsync(new Event(actorName, index, @event));

                await session.SaveChangesAsync();

                return index++;
            }
        }

        public async Task PersistSnapshotAsync(string actorName, long index, object snapshot)
        {
            using (var session = _store.OpenAsyncSession())
            {
                await session.StoreAsync(new Snapshot(actorName, index, snapshot));

                await session.SaveChangesAsync();
            }
        }

#if NET46
        public async Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
#else
        public Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
#endif
        {
            var indexName = "DeleteEventIndex";

            var indexQuery = new IndexQuery { Query = $"ActorName:{actorName} AND Index_Range:[Lx0 TO Lx{inclusiveToIndex}]" };
#if NET46
            Operation operation = await _store.AsyncDatabaseCommands.DeleteByIndexAsync(indexName, indexQuery);
#else
            return Task.CompletedTask;
#endif
        }

#if NET46
        public async Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
#else
        public Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
#endif
        {
            var indexName = "DeleteSnapshotIndex";

            var indexQuery = new IndexQuery { Query = $"ActorName:{actorName} AND Index_Range:[Lx0 TO Lx{inclusiveToIndex}]" };
#if NET46
            Operation operation = await _store.AsyncDatabaseCommands.DeleteByIndexAsync(indexName, indexQuery);
#else
            return Task.CompletedTask;
#endif
        }
    }
}