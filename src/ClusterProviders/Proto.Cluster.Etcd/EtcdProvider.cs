using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Draft;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Proto.Cluster.Etcd
{
    public class EtcdProviderOptions
    {
        public static TimeSpan DefaultServiceTtl = TimeSpan.FromSeconds(3);
        public static TimeSpan DefaultRefreshTtl = TimeSpan.FromSeconds(1);
        public static TimeSpan DefaultDeregisterCritical = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Default value is 3 seconds
        /// </summary>
        public TimeSpan? ServiceTtl { get; set; } = DefaultServiceTtl;

        /// <summary>
        /// Default value is 1 second
        /// </summary>
        public TimeSpan? RefreshTtl { get; set; } = DefaultRefreshTtl;

        /// <summary>
        /// After that time service registration will be removed
        /// </summary>
        /// <remarks>
        /// Default value is 10 seconds
        /// </remarks>
        public TimeSpan? DeregisterCritical { get; set; } = DefaultDeregisterCritical;
    }

    public class EtcdClientOptions
    {
        /// <summary>
        /// Gets or sets the etcd hosts.
        /// </summary>
        /// <value>
        /// The etcd hosts.
        /// </value>
        public Uri[] Hosts { get; set; }

        /// <summary>
        /// Gets or sets the name of the user.
        /// </summary>
        /// <value>
        /// The name of the user.
        /// </value>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the password.
        /// </summary>
        /// <value>
        /// The password.
        /// </value>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the CA certificate.
        /// </summary>
        /// <value>
        /// The CA certificate.
        /// </value>
        public string CaCertificate { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the client certificate.
        /// </summary>
        /// <value>
        /// The client certificate.
        /// </value>
        public string ClientCertificate { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the client key.
        /// </summary>
        /// <value>
        /// The client key.
        /// </value>
        public string ClientKey { get; set; } = string.Empty;
    }

    [DataContract]
    public sealed class ServiceInfo
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "kinds")]
        public string[] Kinds { get; set; }

        [DataMember(Name = "lastAccess")]
        public DateTimeOffset LastAccessTime { get; set; }
    }

    public class EtcdProvider : IClusterProvider
    {
        private static readonly Regex KeyPattern = new Regex(@"^(/?.*)?/(?<host>.*):(?<port>\d+)(/(?<type>.*))?", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private readonly IEtcdClient _client;
        private string _id;
        private string _clusterName;
        private string _address;
        private int _port;
        private string[] _kinds;
        private readonly TimeSpan _serviceTtl;
        private readonly TimeSpan _deregisterCritical;
        private readonly TimeSpan _refreshTtl;
        private readonly ILogger _logger;
        private volatile bool _shutdown;
        private bool _deregistered;
        private IMemberStatusValue _statusValue;
        private IMemberStatusValueSerializer _statusValueSerializer;
        private volatile string[] _clusterItems;

        public EtcdProvider(EtcdProviderOptions options, ILogger logger) : this(options, config => { }, logger) { }

        public EtcdProvider(EtcdProviderOptions options, Action<EtcdClientOptions> storageConfig, ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceTtl = options.ServiceTtl ?? EtcdProviderOptions.DefaultServiceTtl;
            _refreshTtl = options.RefreshTtl ?? EtcdProviderOptions.DefaultRefreshTtl;
            _deregisterCritical = options.DeregisterCritical ?? EtcdProviderOptions.DefaultDeregisterCritical;
            var clientOptions = new EtcdClientOptions();
            storageConfig?.Invoke(clientOptions);

            var pool = Draft.Endpoints.EndpointPool.Build()
                .WithRoutingStrategy(Draft.Endpoints.EndpointRoutingStrategy.RoundRobin)
                .WithVerificationStrategy(Draft.Endpoints.EndpointVerificationStrategy.All)
                .VerifyAndBuild(clientOptions.Hosts)
                .GetAwaiter()
                .GetResult();

            _client = Draft.Etcd.ClientFor(pool);
        }

        public EtcdProvider(IOptions<EtcdProviderOptions> options, ILogger logger) : this(options.Value, config => { }, logger)
        {
        }

        public EtcdProvider(IOptions<EtcdProviderOptions> options, Action<EtcdClientOptions> storageConfig, ILogger logger) : this(options.Value, storageConfig, logger)
        {
        }

        public async Task RegisterMemberAsync(string clusterName, string address, int port, string[] kinds, IMemberStatusValue statusValue, IMemberStatusValueSerializer statusValueSerializer)
        {
            _id = $"{clusterName}_{address}_{port}";
            _clusterName = clusterName;
            _address = address;
            _port = port;
            _kinds = kinds;
            _statusValue = statusValue;
            _statusValueSerializer = statusValueSerializer;

            await RegisterServiceAsync();
            await RegisterMemberValsAsync();

            UpdateTtl();
        }

        public async Task DeregisterMemberAsync()
        {
            //DeregisterService
            await DeregisterServiceAsync();

            _deregistered = true;
        }

        public async Task Shutdown()
        {
            _shutdown = true;
            if (!_deregistered)
                await DeregisterMemberAsync();
        }

        public string[] ClusterAddresses => _clusterItems;

        public void MonitorMemberStatusChanges()
        {
            var t = new Thread(_ =>
            {
                while (!_shutdown)
                {
                    NotifyStatuses().Wait();
                    Thread.Sleep(_refreshTtl);
                }
            }) {IsBackground = true};
            t.Start();
        }

        private void UpdateTtl()
        {
            var t = new Thread(_ =>
            {
                while (!_shutdown)
                {
                    BlockingUpdateTtl().Wait();
                    Thread.Sleep(_refreshTtl);
                }
            }) {IsBackground = true};
            t.Start();
        }

        private async Task RegisterServiceAsync()
        {
            await _client
                .CreateDirectory($"{_clusterName}/{_address}:{_port}")
                .WithTimeToLive(_deregisterCritical)
                .Execute()
                .ConfigureAwait(false);

            await _client
                .UpsertKey($"{_clusterName}/{_address}:{_port}/Id")
                .WithValue(JsonConvert.SerializeObject(new ServiceInfo { Id = _id, Kinds = _kinds, LastAccessTime = DateTimeOffset.UtcNow }))
                .WithTimeToLive(_deregisterCritical)
                .Execute()
                .ConfigureAwait(false);
        }

        private async Task DeregisterServiceAsync()
        {
            await _client.DeleteDirectory($"{_clusterName}/{_address}:{_port}").Execute().ConfigureAwait(false);
        }

        public async Task UpdateMemberStatusValueAsync(IMemberStatusValue statusValue)
        {
            _statusValue = statusValue;

            if (_statusValue == null) return;
            
            if (string.IsNullOrEmpty(_id)) return;

            //register a semi unique ID for the current process
            var kvKey = $"{_clusterName}/{_address}:{_port}/StatusValue"; //slash should be present
            var statusValValue = Convert.ToBase64String(_statusValueSerializer.ToValueBytes(_statusValue));
            await _client
                .UpsertKey(kvKey)
                .WithValue(statusValValue)
                .WithTimeToLive(_serviceTtl)
                .Execute()
                .ConfigureAwait(false);
        }

        private async Task RegisterMemberValsAsync()
        {
            if (_statusValue != null)
            {
                var statusValValue = Convert.ToBase64String(_statusValueSerializer.ToValueBytes(_statusValue));
                await _client
                    .UpsertKey($"{_clusterName}/{_address}:{_port}/StatusValue")
                    .WithValue(statusValValue)
                    .WithTimeToLive(_serviceTtl)
                    .Execute()
                    .ConfigureAwait(false);
            }
        }

        private async Task NotifyStatuses()
        {
            var items = await _client.GetKey($"{_clusterName}/").WithRecursive().Execute().ConfigureAwait(false);
            if (items?.Data == null || !items.Data.IsDir)
            {
                _logger.LogError($"Cluster root key '{_clusterName}' is not available in ETCD storage!");
                return;
            }

            var memberStatuses = new List<MemberStatus>();
            foreach (var dataChild in items.Data.Children)
            {
                var keyMatch = KeyPattern.Match(dataChild.Key);
                if (!keyMatch.Success)
                {
                    continue;
                }

                var host = keyMatch.Groups["host"].Value;
                var port = int.Parse(keyMatch.Groups["port"].Value);
                string memberId = null;
                byte[] memberStatusValue = null;
                string[] kinds = null;
                if (dataChild.Children != null)
                {
                    foreach (var childItem in dataChild.Children)
                    {
                        keyMatch = KeyPattern.Match(childItem.Key);
                        if (!keyMatch.Success)
                        {
                            continue;
                        }

                        var type = keyMatch.Groups["type"].Value.ToUpper();
                        switch (type)
                        {
                            case "ID":
                                var info = JsonConvert.DeserializeObject<ServiceInfo>(childItem.RawValue);
                                memberId = info.Id;
                                kinds = info.Kinds;
                                break;
                            case "STATUSVALUE":
                                memberStatusValue = Convert.FromBase64String(childItem.RawValue);
                                break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(memberId))
                {
                    _logger.LogWarning($"Cluster member id is null! Key is '{dataChild.Key}'.");
                    continue;
                }

                if (kinds == null)
                {
                    _logger.LogWarning($"Cluster kinds is null! Key is '{dataChild.Key}'.");
                    continue;
                }

                memberStatuses.Add(new MemberStatus(memberId, host, port, kinds, true, memberStatusValue != null ? _statusValueSerializer.FromValueBytes(memberStatusValue) : null));
            }

            _clusterItems = memberStatuses.Select(x => x.Address).ToArray();
            //Update Tags for this member
            foreach (var memStat in memberStatuses)
            {
                if (memStat.Address == _address && memStat.Port == _port)
                {
                    _kinds = memStat.Kinds.ToArray();
                    break;
                }
            }

            var res = new ClusterTopologyEvent(memberStatuses);
            Actor.EventStream.Publish(res);
        }

        private async Task BlockingUpdateTtl()
        {
            await _client
                .UpdateDirectory($"{_clusterName}/{_address}:{_port}")
                .WithTimeToLive(_deregisterCritical)
                .Execute()
                .ConfigureAwait(false);

            await _client
                .UpsertKey($"{_clusterName}/{_address}:{_port}/Id")
                .WithValue(JsonConvert.SerializeObject(new ServiceInfo { Id = _id, Kinds = _kinds, LastAccessTime = DateTimeOffset.UtcNow }))
                .WithTimeToLive(_deregisterCritical)
                .Execute()
                .ConfigureAwait(false);
        }
    }
}