﻿// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto;

public class Program
{
    static void Main(string[] args)
    {
        var props = Actor.FromProducer(() => new ProcessActor());
        var pid = Actor.Spawn(props);
        pid.Tell(new Process());
        Task.Run(async () =>
        {
            await Task.Delay(50);
            pid.Stop();
        });
        Console.ReadLine();
    }

    internal class Process { }

    internal class ProcessActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    Console.WriteLine("Started");
                    break;

                case Process _:
                    Console.WriteLine("Process");
                    context.Self.Tell(new Process());
                    break;

                case Stopping _:
                    Console.WriteLine("Stopping");
                    break;

                case Stopped _:
                    Console.WriteLine("Stopped");
                    break;
            }
            return Actor.Done;
        }
    }
}