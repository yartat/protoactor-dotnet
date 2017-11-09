// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.IMDG;
using Proto.Remote;
using ProtosReflection = Proto.Cluster.ProtosReflection;

internal class Program
{
    private static void Main(string[] args)
    {
        StartConsulDevMode();
        Serialization.RegisterFileDescriptor(Proto.IMDG.PList.ProtosReflection.Descriptor);

        Cluster.Start("MyCluster", "127.0.0.1", 0, new ConsulProvider(new ConsulProviderOptions()));
        var list = DataGrid.GetList<string>("MyList");
        var count1 = list.CountAsync().Result;
        Console.WriteLine(count1);
        for (var i = 0; i < 1000; i++)
        {
            list.AddAsync(i.ToString()).Wait();
        }
        var count2 = list.CountAsync().Result;
        Console.WriteLine(count2);

        Console.ReadLine();
        Console.WriteLine("Shutting Down...");
        Cluster.Shutdown();
    }

    private static void StartConsulDevMode()
    {
        Console.WriteLine("Consul - Starting");
        var psi =
            new ProcessStartInfo(@"..\..\..\dependencies\consul",
                "agent -server -bootstrap -data-dir /tmp/consul -bind=127.0.0.1 -ui")
            {
                CreateNoWindow = true
            };
        Process.Start(psi);
        Console.WriteLine("Consul - Started");
    }
}