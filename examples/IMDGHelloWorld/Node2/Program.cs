// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.IMDG;
using Proto.Remote;

namespace Node2
{
    class Program
    {
        static void Main(string[] args)
        {
            Remote.RegisterKnownKind("PList", ListActor.Props);
            Cluster.Start("MyCluster", "127.0.0.1", 0, new ConsulProvider(new ConsulProviderOptions()));
            Console.ReadLine();
            Console.WriteLine("Shutting Down...");
            Cluster.Shutdown();
        }
    }
}