using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.IMDG.PList;
using Proto.Remote;

namespace Proto.IMDG
{
    public class ListProxy<T> : ICollection<T>
    {
        private readonly string _name;

        public ListProxy(string name)
        {
            _name = name;
        }

        public IEnumerator<T> GetEnumerator() => throw new NotSupportedException("Cannot enumerate distributed lists");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(T item) => AddAsync(item);

        public void Clear() => ClearAsync();

        public bool Contains(T item) => throw new NotImplementedException();

        public void CopyTo(T[] array, int arrayIndex) => throw new NotImplementedException();

        public bool Remove(T item)
        {
            RemoveAsync(item);
            return false;
        }

        public int Count => CountAsync().Result;
        public bool IsReadOnly => false;


        public async Task AddAsync(T item)
        {
            var pid = await GetPid();
            pid.Tell(new AddRequest
            {
                Value = PSerializer.Serialize(item)
            });
        }

        public async Task<int> CountAsync()
        {
            var pid = await GetPid();
            var res = await pid.RequestAsync<CountResponse>(new CountRequest());
            return res.Value;
        }

        public async Task<T> GetAsync(int index)
        {
            var pid = await GetPid();
            var res = await pid.RequestAsync<GetResponse>(new GetRequest {Index = index});
            return PSerializer.Deserialize<T>(res.Value);
        }

        private async Task<PID> GetPid()
        {
            for (var i = 0; i < 100; i++)
            {
                try
                {
                    var (pid, status) = await Cluster.Cluster.GetAsync(_name, "PList");
                    if (status == ResponseStatusCode.OK || status == ResponseStatusCode.ProcessNameAlreadyExist)
                        return pid;
                }
                catch
                {
                }
                await Task.Delay(i * 50);
            }
            throw new Exception("Retry error");
        }

        public async Task ClearAsync()
        {
            var pid = await GetPid();
            pid.Tell(new ClearRequest());
        }

        private async Task RemoveAsync(T item)
        {
            var pid = await GetPid();
            pid.Tell(new RemoveRequest
            {
                Value = PSerializer.Serialize(item)
            });
        }
    }
}