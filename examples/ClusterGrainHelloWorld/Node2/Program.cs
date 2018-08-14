// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Messages;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.WeightedMemberStrategy;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

namespace Node2
{
    public class HelloGrain : IHelloGrain
    {
        public Task<HelloResponse> SayHello(HelloRequest request)
        {
            return Task.FromResult(new HelloResponse
            {
                Message = "Hello from typed grain"
            });
        }
    }
    public class Program
    {
        public static int Port;
        static void Main(string[] args)
        {
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            Port = 12000;
            if (args.Length >= 1)
            {
                Port = int.TryParse(args[0], out var portValue) ? portValue : 12000;
            }

            Grains.HelloGrainFactory(() => new HelloGrain());

            Cluster.Start("MyCluster", "127.0.0.1", Port, new ConsulProvider(new ConsulProviderOptions()));
            Console.WriteLine("Started.");
            Console.ReadLine();
            Console.WriteLine("Shutting Down...");
            Cluster.Shutdown();
        }
    }
}