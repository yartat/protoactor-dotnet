﻿// -----------------------------------------------------------------------
//   <copyright file="IClusterProvider.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;

namespace Proto.Cluster
{
    public interface IClusterProvider
    {
        string[] ClusterAddresses { get; }
        Task RegisterMemberAsync(string clusterName, string hostName, int port, string[] kinds, IMemberStatusValue statusValue, IMemberStatusValueSerializer serializer);
        void MonitorMemberStatusChanges();
        Task UpdateMemberStatusValueAsync(IMemberStatusValue statusValue);
        Task DeregisterMemberAsync();
        Task Shutdown();
    }
}