﻿// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using Messages;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

class Program
{
    static void Main(string[] args)
    {
        ConsulProvider.StartConsulDevMode(@"..\..\..\dependencies\consul");
        Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
        Cluster.Start("MyCluster", "127.0.0.1", 12001, new ConsulProvider(new ConsulProviderOptions()));

        var client = Grains.HelloGrain("TheName");

        var res = client.SayHello(new HelloRequest()).Result;
        Console.WriteLine(res.Message);
        Console.ReadLine();
        res = client.SayHello(new HelloRequest()).Result;
        Console.WriteLine(res.Message);
        Console.ReadLine();
    }
}