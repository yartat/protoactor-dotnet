using System;
using System.Threading.Tasks;
using Messages;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;

namespace Client.Proto
{
    public class ProtoCluster : ICluster
    {
        public ProtoCluster(string clusterName, int port, Uri consulUri)
        {
            Serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
            Cluster.Start(clusterName, "127.0.0.1", port, new ConsulProvider(new ConsulProviderOptions(), c => c.Address = consulUri));
            WaitForUpdateNodes().Wait();
        }

        public Task<DepositResponse> MakeDeposit(string id, string type, DepositRequest request)
        {
            return Invoke<DepositRequest, DepositResponse>(id, type, request);
        }

        private async Task<TResponse> Invoke<TRequest, TResponse>(string id, string type, TRequest request)
        {
            var (pid, sc) = await Cluster.GetAsync(id, type).ConfigureAwait(false);
            return await pid.RequestAsync<TResponse>(request).ConfigureAwait(false);
        }

        private async Task WaitForUpdateNodes()
        {
            var (t, s) = await Cluster.GetAsync("ping", "Player").ConfigureAwait(false);
            while (t == null)
            {
                (t, s) = await Cluster.GetAsync("ping", "Player").ConfigureAwait(false);
            }
        }
    }
}