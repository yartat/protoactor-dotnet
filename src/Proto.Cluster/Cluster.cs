// -----------------------------------------------------------------------
//   <copyright file="Cluster.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Cluster
{
    public static class Cluster
    {
        private static readonly ILogger Logger = Log.CreateLogger(typeof(Cluster).FullName);
        internal static ClusterConfig Config;

        /// <summary>
        /// Starts the specified cluster.
        /// </summary>
        /// <param name="clusterName">Name of the cluster.</param>
        /// <param name="address">The address.</param>
        /// <param name="port">The port.</param>
        /// <param name="cp">The cp.</param>
        /// <param name="advertisedHostname">The advertised hostname.</param>
        /// <param name="advertisedPort">The advertised port.</param>
        /// <param name="memberStrategyBuilder">The member strategy builder.</param>
        public static void Start(
            string clusterName, 
            string address, 
            int port, 
            IClusterProvider cp,
            string advertisedHostname = null,
            int? advertisedPort = null, 
            Func<string, IMemberStrategy> memberStrategyBuilder = null) => 
            StartWithConfig(new ClusterConfig(clusterName, address, port, cp, advertisedHostname, advertisedPort, memberStrategyBuilder));

        /// <summary>
        /// Starts cluster the with configuration.
        /// </summary>
        /// <param name="config">The configuration.</param>
        public static void StartWithConfig(ClusterConfig config)
        {
            Config = config;

            if (Config.Address == "0.0.0.0" && string.IsNullOrEmpty(Config.RemoteConfig.AdvertisedHostname))
            {
                Config.RemoteConfig.AdvertisedHostname = GetLocalIpAddress().ToString();
            }

            if (Config.RemoteConfig.AdvertisedPort == null && !string.IsNullOrEmpty(Config.RemoteConfig.AdvertisedHostname))
            {
                Config.RemoteConfig.AdvertisedPort = Config.Port;
            }

            Remote.Remote.Start(Config.Address, Config.Port, Config.RemoteConfig);
        
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            Logger.LogInformation("Starting cluster");
            var hostAddress = Config.RemoteConfig.AdvertisedHostname;
            var hostPort = Config.RemoteConfig.AdvertisedPort;
            var (h, p) = ParseAddress(ProcessRegistry.Instance.Address);
            if (string.IsNullOrEmpty(hostAddress))
            {
                hostAddress = h;
            }

            if (hostPort == null)
            {
                hostPort = p;
            }

            var kinds = Remote.Remote.GetKnownKinds();
            Partition.Setup(kinds);
            PidCache.Setup();
            MemberList.Setup();
            Config.ClusterProvider.RegisterMemberAsync(Config.Name, hostAddress, hostPort.Value, kinds, config.InitialMemberStatusValue, config.MemberStatusValueSerializer).Wait();
            Config.ClusterProvider.MonitorMemberStatusChanges();

            Logger.LogInformation("Cluster was started successfully");
        }

        /// <summary>
        /// Shutdowns cluster gracefully.
        /// </summary>
        /// <param name="gracefull">if set to <c>true</c> [gracefully].</param>
        public static void Shutdown(bool gracefull = true)
        {
            if (gracefull)
            {
                //This is to wait ownership transferring complete.
                if (Config?.ClusterProvider != null)
                {
                    var tasks = Config.ClusterProvider.ClusterAddresses.Select(x => Remote.Remote.SpawnShutdown(x, TimeSpan.FromSeconds(3)));
                    Task.WhenAll(tasks).GetAwaiter().GetResult();
                    Config.ClusterProvider.Shutdown();
                }

                MemberList.Stop();
                PidCache.Stop();
                Partition.Stop();
            }

            Remote.Remote.Shutdown(gracefull);

            Logger.LogInformation("Stopped Cluster");
        }

        private static (string host, int port) ParseAddress(string address)
        {
            //TODO: use correct parsing
            var parts = address.Split(':');
            var host = parts[0];
            var port = int.Parse(parts[1]);
            return (host, port);
        }

        /// <summary>
        /// Gets the asynchronous.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="kind">The kind.</param>
        /// <returns></returns>
        public static Task<(PID, ResponseStatusCode)> GetAsync(string name, string kind) => GetAsync(name, kind, CancellationToken.None);

        /// <summary>
        /// Gets the asynchronous.
        /// </summary>
        /// <param name="name">The actor name.</param>
        /// <param name="kind">The actor kind.</param>
        /// <param name="ct">The cancellation token instance.</param>
        /// <returns></returns>
        public static async Task<(PID, ResponseStatusCode)> GetAsync(string name, string kind, CancellationToken ct)
        {
            //Check Cache
            if (PidCache.TryGetCache(name, out var pid))
                return (pid, ResponseStatusCode.OK);

            //Get Pid
            var address = MemberList.GetPartition(name, kind);

            if (string.IsNullOrEmpty(address))
            {
                return (null, ResponseStatusCode.Unavailable);
            }

            var remotePid = Partition.PartitionForKind(address, kind);
            var req = new ActorPidRequest
            {
                Kind = kind,
                Name = name
            };

            try
            {
                var resp = ct == CancellationToken.None
                           ? await RootContext.Empty.RequestAsync<ActorPidResponse>(remotePid, req, Config.TimeoutTimespan)
                           : await RootContext.Empty.RequestAsync<ActorPidResponse>(remotePid, req, ct);
                var status = (ResponseStatusCode) resp.StatusCode;
                switch (status)
                {
                    case ResponseStatusCode.OK:
                        PidCache.TryAddCache(name, resp.Pid);
                        return (resp.Pid, status);
                    default:
                        return (resp.Pid, status);
                }
            }
            catch(TimeoutException)
            {
                return (null, ResponseStatusCode.Timeout);
            }
            catch
            {
                return (null, ResponseStatusCode.Error);
            }
        }

        /// <summary>
        /// Removes actor name from the cache.
        /// </summary>
        /// <param name="name">The actor name.</param>
        public static void RemoveCache(string name) => PidCache.RemoveCacheByName(name);

        private static IPAddress GetLocalIpAddress()
        {
            var host = Dns.GetHostEntryAsync(Dns.GetHostName()).GetAwaiter().GetResult();
            var result = host.AddressList.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
            return result ?? throw new Exception("No network adapters with an IPv4 address in the system!");
        }
    }
}