// -----------------------------------------------------------------------
//   <copyright file="RemoteProcess.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Remote
{
    public class RemoteProcess : Process
    {
        private readonly PID _pid;

        public RemoteProcess(PID pid)
        {
            _pid = pid;
        }

        protected override void SendUserMessage(PID _, object message) => Send(message);

        protected override void SendSystemMessage(PID _, object message) => Send(message);

        private void Send(object msg)
        {
            switch (msg)
            {
                case Watch w:
                    var rw = new RemoteWatch(w.Watcher, _pid);
                    EndpointManager.RemoteWatch(rw);
                    break;
                case Unwatch uw:
                    var ruw = new RemoteUnwatch(uw.Watcher, _pid);
                    EndpointManager.RemoteUnwatch(ruw);
                    break;
                default:
                    Remote.SendMessage(_pid, msg,-1);
                    break;
            }
        }
    }
}