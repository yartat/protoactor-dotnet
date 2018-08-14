// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Messages;
using Newtonsoft.Json;
using Node2.Contracts;
using Node2.Storage;
using Node2.Storage.Elastic;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Etcd;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

namespace Node2
{
    public class PlayerActor : IActor
    {
        private static long Items = 0;
        private readonly ElasticRepository _playerRepository;
        private readonly ElasticRepository _depositRepository;

        private Dictionary<string, double> Balances { get; set; }

        public PlayerActor(ElasticRepository playerRepository, ElasticRepository depositRepository)
        {
            _playerRepository = playerRepository;
            _depositRepository = depositRepository;
        }

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case DepositRequest request:
                    if (Balances == null)
                    {
#if PERFORMANCE_TEST
                        Balances = new Dictionary<string, double>();
#else
                        var balanceDoc = await _playerRepository.GetDocumentAsync(context.Self.Id).ConfigureAwait(false);
                        Balances = balanceDoc != null
                            ? JsonConvert.DeserializeObject<Dictionary<string, double>>(balanceDoc.Value)
                            : new Dictionary<string, double>();
#endif

                    }

#if PERFORMANCE_TEST
                    StorageDataItem depositDocument = null;
#else
                    var depositDocument = await _depositRepository.GetDocumentAsync(request.Id).ConfigureAwait(false);
#endif
                    if (depositDocument != null)
                    {
                        var item = JsonConvert.DeserializeObject<DepositTransaction>(depositDocument.Value);
                        var r = new DepositResponse
                        {
                            Id = item.TransactionId,
                            AlreadyProcessed = true
                        };
                        r.Balances.Add(new Dictionary<string,double> { { "1", 1.0 } });
                        context.Respond(r);
                        break;
                    }

                    Balances.TryGetValue(request.Currency, out var value);
                    value += request.Amount;
                    Balances[request.Currency] = value;

                    var deposit = new DepositTransaction
                    {
                        KioskId = request.Kiosk,
                        Manual = request.Manual,
                        Amount = (decimal) request.Amount,
                        CreatedOn = request.Date.ToDateTime(),
                        CurrencyCode = request.Currency,
                        PlayerId = request.PlayerId,
                        TransactionId = request.Id
                    };

#if !PERFORMANCE_TEST
                    var playerTask = _playerRepository.UpsertDocumentAsync(context.Self.Id,
                        new StorageDataItem {Value = JsonConvert.SerializeObject(Balances)});
                    var depositTask = _depositRepository.UpsertDocumentAsync(request.Id,
                        new StorageDataItem {Value = JsonConvert.SerializeObject(deposit)});

                    await Task.WhenAll(playerTask, depositTask).ConfigureAwait(false);
                    var currenctItemCount = Interlocked.Increment(ref Items);
                    if (currenctItemCount % 100 == 0)
                    {
                        Console.WriteLine($"Processed {currenctItemCount} requests");
                    }
#endif
                    var response = new DepositResponse
                    {
                        Id = request.Id,
                    };
                    response.Balances.Add(Balances);
                    context.Respond(response);
                    break;
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            var options = new ElasticOptions
            {
                IndexNamePrefix = "Wallet_new",
                NumberOfReplicas = 3,
                NumberOfShards = 3,
                Url = "http://localhost:9200"
                //Url = "http://elastic.betlab.private:9200"
            };
            var client = new ElasticSimpleClient(new Uri(options.Url));
            var player = new ElasticRepository(client, options, "player");
            var deposit = new ElasticRepository(client, options, "deposit");
            var props = Props.FromProducer(() => new PlayerActor(player, deposit));

            var parsedArgs = parseArgs(args);
            Remote.RegisterKnownKind("Player", props);
            Cluster.Start("MyCluster", parsedArgs.ServerName, parsedArgs.Port, new EtcdProvider(new EtcdProviderOptions(), opt => opt.Hosts = new[] { new Uri("http://192.168.1.102:2379") }));
            Console.WriteLine("Started.");
            var exitEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                exitEvent.Set();
            };

            Console.WriteLine("Press CTRL+C to stop node and exit application");

            exitEvent.WaitOne();
            Console.WriteLine("Shutdown.");
            Cluster.Shutdown();
        }

        private static Node2Config parseArgs(string[] args)
        {
            if(args.Length >= 3) 
            {
                return new Node2Config(args[0], args[1], args[2]);
            }

            if (args.Length >= 2)
            {
                return new Node2Config(args[0], args[1], "0.0.0.0");
            }

            if (args.Length >= 1)
            {
                return new Node2Config(args[0], "0.0.0.0", "127.0.0.1");
            }
            return new Node2Config("12000", "0.0.0.0", "127.0.0.1");
        }

        class Node2Config
        {
            public string ServerName { get; }
            public string ConsulUrl { get; }
            public int Port { get; }

            public Node2Config(string port, string serverName, string consulUrl)
            {
                Port = int.Parse(port);
                ServerName = serverName;
                ConsulUrl = consulUrl;
            }
        }
    }
}