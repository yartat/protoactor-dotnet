﻿// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Messages;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Etcd;
using Proto.Remote;
using Process = System.Diagnostics.Process;
using ProtosReflection = Messages.ProtosReflection;

class Program
{
    private const int ItemsCount = 100000;
    private const int ProcessingCount = 1000000;

    private static readonly List<string> ItemNames = new List<string>(ItemsCount + 1);

    static async Task Main(string[] args)
    {
        for (int i = 0; i < ItemsCount; ++i)
        {
            ItemNames.Add(Guid.NewGuid().ToString());
        }

        Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
        var parsedArgs = parseArgs(args);
        Cluster.Start("MyCluster", parsedArgs.ServerName, 12001, new EtcdProvider(new EtcdProviderOptions(), options => options.Hosts = new []{ new Uri("http://192.168.1.102:2379") }));
        var random = new Random();
        var (t, s) = await Cluster.GetAsync(ItemNames[0], "Player").ConfigureAwait(false);
        while (t == null)
        {
            (t, s) = await Cluster.GetAsync(ItemNames[0], "Player").ConfigureAwait(false);
        }

        var sw = new Stopwatch();
        sw.Start();
        for(var i = 0; i <= ProcessingCount; ++i)
        {
            var playerId = ItemNames[random.Next(ItemsCount)];
            var res = await Invoke<DepositRequest, DepositResponse>(playerId, new DepositRequest
            {
                Amount = 1,
                Currency = "UAH",
                Date = Timestamp.FromDateTime(DateTime.UtcNow),
                Id = Guid.NewGuid().ToString(),
                Kiosk = "Web",
                PlayerId = playerId
            }).ConfigureAwait(false);
            if (i % 100 == 0)
            {
                Console.CursorLeft = 0;
                Console.Write($"Processed items: {i}");
            }
        }

        sw.Stop();
        Console.WriteLine("Shutting Down...");
        Console.WriteLine($"Processing time is {sw.Elapsed}. Perfromance is {ProcessingCount / sw.ElapsedMilliseconds * 1000} items/sec.");
        Console.WriteLine("Press key");
        Console.ReadLine();
    }

    private static async Task<TResult> Invoke<TRequest, TResult>(string playerId, TRequest request)
    {
        while (true)
        {
            var (pid, sc) = await Cluster.GetAsync(playerId, "Player").ConfigureAwait(false);
            while (pid == null)
            {
                (pid, sc) = await Cluster.GetAsync(playerId, "Player").ConfigureAwait(false);
            }

            try
            {
                return await RootContext.Empty.RequestAsync<TResult>(pid, request).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception: {exception.Message}");
            }
        }
    }

    private static void StartConsulDevMode()
    {
        Console.WriteLine("Consul - Starting");
        ProcessStartInfo psi =
            new ProcessStartInfo(@"..\..\..\..\..\dependencies\consul",
                "agent -server -bootstrap -data-dir /tmp/consul -bind=127.0.0.1 -ui")
            {
                CreateNoWindow = true,
            };
        Process.Start(psi);
        Console.WriteLine("Consul - Started");
    }

    private static Node1Config parseArgs(string[] args)
    {
        if(args.Length > 0) 
        {
            return new Node1Config(args[0], args[1], bool.Parse(args[2]));
        }
        return new Node1Config("0.0.0.0", "127.0.0.1", true);
    }

    class Node1Config
    {
        public string ServerName { get; }
        public string ConsulUrl { get; }
        public bool StartConsul { get; }
        public Node1Config(string serverName, string consulUrl, bool startConsul) 
        {
            ServerName = serverName;
            ConsulUrl = consulUrl;
            StartConsul = startConsul;
        }
    }
}