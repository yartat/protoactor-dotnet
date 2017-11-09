using System;
using System.IO;
using System.Threading.Tasks;
using Google.Protobuf;

using Proto.IMDG.PList;
using Proto.Remote;
using Wire;


namespace Proto.Cluster.IMDG
{
    public class GridList<T>
    {
        private static Serializer Serializer = new Serializer(new SerializerOptions(false,true));
        private readonly string _name;

        public GridList(string name)
        {
            _name = name;
        }

        public async Task AddAsync(T item)
        {
            var pid = await GetPid();
            pid.Tell(new AddRequest
            {
                Value = Serialize(item)
            });
        }

        public async Task<int> CountAsync()
        {
            var pid = await GetPid();
            var res = await pid.RequestAsync<CountResponse>(new CountRequest());
            return res.Value;
        }

        public async Task<T> Get(int index)
        {
            var pid = await GetPid();
            var res = await pid.RequestAsync<GetResponse>(new GetRequest {Index = index});
            return Deserialize(res.Value);
        }

        private static PObject Serialize(T item)
        {
            using (var ms = new MemoryStream())
            {
                Serializer.Serialize(item, ms);
                ms.Position = 0;
                return new PObject()
                {                    
                    Payload = ByteString.FromStream(ms),
                };
            }
            
        }

        private static T Deserialize(PObject self)
        {
            using (var ms = new MemoryStream(self.Payload.ToByteArray()))
            {
                var res = Serializer.Deserialize<T>(ms);
                return res;
            }
        }

        private async Task<PID> GetPid()
        {
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    var (pid, status) = await Cluster.GetAsync(_name, "GridList");
                    if (status != ResponseStatusCode.OK && status != ResponseStatusCode.ProcessNameAlreadyExist)
                    {
                        return pid;
                    }
                }
                catch
                {
                    await Task.Delay(i * 50);
                }
            }
            throw new Exception("Retry error");
        }
    }
}