using System;
using System.Threading.Tasks;
using Messages;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;

namespace Client.Proto
{
    /// <summary>
    /// The cluster client implementation
    /// </summary>
    /// <seealso cref="Client.Proto.ICluster" />
    public class ProtoCluster : ICluster
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProtoCluster"/> class.
        /// </summary>
        /// <param name="clusterName">Name of the cluster.</param>
        /// <param name="port">The port.</param>
        /// <param name="consulUri">The consul URI.</param>
        public ProtoCluster(string clusterName, int port, Uri consulUri)
        {
            Serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
            Cluster.Start(clusterName, "127.0.0.1", port, new ConsulProvider(new ConsulProviderOptions(), c => c.Address = consulUri));
            WaitForUpdateNodes().Wait();
        }

        /// <inheritdoc />
        public Task<DepositResponse> MakeDeposit(string id, string type, DepositRequest request)
        {
            return Invoke<DepositRequest, DepositResponse>(id, type, request);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Cluster.Shutdown();
        }

        private static async Task<TResponse> Invoke<TRequest, TResponse>(string id, string type, TRequest request)
        {
            var (pid, _) = await Cluster.GetAsync(id, type).ConfigureAwait(false);
            return await pid.RequestAsync<TResponse>(request).ConfigureAwait(false);
        }

        private static async Task WaitForUpdateNodes()
        {
            var (t, _) = await Cluster.GetAsync("ping", "Player").ConfigureAwait(false);
            while (t == null)
            {
                (t, _) = await Cluster.GetAsync("ping", "Player").ConfigureAwait(false);
            }
        }
    }
}