// -----------------------------------------------------------------------
//   <copyright file="Cluster.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
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

        internal static ClusterConfig Configuration;

        public static void Start(
            string clusterName, 
            string address, 
            int port, 
            IClusterProvider cp,
            string advertisedHostname = null,
            int? advertisedPort = null, 
            Func<string, IMemberStrategy> memberStrategyBuilder = null) => 
            StartWithConfig(new ClusterConfig(clusterName, address, port, cp, advertisedHostname, advertisedPort, memberStrategyBuilder));

        public static void StartWithConfig(ClusterConfig config)
        {
            Configuration = config;

            if (Configuration.Address == "0.0.0.0" && string.IsNullOrEmpty(Configuration.RemoteConfig.AdvertisedHostname))
            {
                Configuration.RemoteConfig.AdvertisedHostname = GetLocalIpAddress().ToString();
            }

            if (Configuration.RemoteConfig.AdvertisedPort == null && !string.IsNullOrEmpty(Configuration.RemoteConfig.AdvertisedHostname))
            {
                Configuration.RemoteConfig.AdvertisedPort = Configuration.Port;
            }

            Remote.Remote.Start(Configuration.Address, Configuration.Port, Configuration.RemoteConfig);
        
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            Logger.LogInformation("Starting cluster");
            var hostAddress = Configuration.RemoteConfig.AdvertisedHostname;
            var hostPort = Configuration.RemoteConfig.AdvertisedPort;
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
            Configuration.ClusterProvider.RegisterMemberAsync(Configuration.Name, hostAddress, hostPort.Value, kinds, config.InitialMemberStatusValue, config.MemberStatusValueSerializer).Wait();
            Configuration.ClusterProvider.MonitorMemberStatusChanges();

            Logger.LogInformation("Cluster was started successfully");
        }

        public static void Shutdown(bool gracefull = true)
        {
            if (gracefull)
            {
                Configuration.ClusterProvider.Shutdown();
                //This is to wait ownership transferring complete.
                Task.Delay(2000).Wait();
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

        public static Task<(PID, ResponseStatusCode)> GetAsync(string name, string kind) => GetAsync(name, kind, CancellationToken.None);

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
                           ? await remotePid.RequestAsync<ActorPidResponse>(req, Configuration.TimeoutTimespan)
                           : await remotePid.RequestAsync<ActorPidResponse>(req, ct);
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

        public static void RemoveCache(string name) => PidCache.RemoveCacheByName(name);

        private static IPAddress GetLocalIpAddress()
        {
            var host = Dns.GetHostEntryAsync(Dns.GetHostName()).GetAwaiter().GetResult();
            var result = host.AddressList.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
            return result ?? throw new Exception("No network adapters with an IPv4 address in the system!");
        }
    }
}