// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Messages;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.WeightedMemberStrategy;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

class Program
{
    static async Task Main(string[] args)
    {
        const int TestCount = 1000000;
        //StartConsulDevMode();
        Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);

        Cluster.Start("MyCluster", "127.0.0.1", 12001, new ConsulProvider(new ConsulProviderOptions()));
        Console.ReadLine();

        var sw = new Stopwatch();
        sw.Start();
        for (int i = 0; i < TestCount; i++)
        {
            var client = Grains.HelloGrain($"Roger{i}");
            var res = await client.SayHello(new HelloRequest());
            if (i % 50000 == 0)
            {
                Console.WriteLine($"Processed {i} items");
            }
        }
        sw.Stop();
        Console.WriteLine($"Processed time is {sw.Elapsed}, performance is {(TestCount * 1.0) / sw.ElapsedMilliseconds * 1000} items per sec");
        Console.ReadLine();
        Console.WriteLine("Shutting Down...");
        Cluster.Shutdown();
    }

    private static void StartConsulDevMode()
    {
        Console.WriteLine("Consul - Starting");
        ProcessStartInfo psi =
            new ProcessStartInfo(@"..\..\..\..\..\..\dependencies\consul",
                "agent -server -bootstrap -data-dir /tmp/consul -bind=127.0.0.1 -ui")
            {
                CreateNoWindow = true,
            };
        Process.Start(psi);
        Console.WriteLine("Consul - Started");
    }
}