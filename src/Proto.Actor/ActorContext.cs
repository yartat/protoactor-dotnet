// -----------------------------------------------------------------------
//   <copyright file="ActorContext.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Mailbox;
using static Proto.Actor;

namespace Proto
{
    internal enum ContextState
    {
        None,
        Alive,
        Restarting,
        Stopping,
        Stopped,
    }

    public class ActorContext : IMessageInvoker, IContext, ISupervisor
    {
        public static readonly IReadOnlyCollection<PID> EmptyChildren = new List<PID>();
        private readonly Func<IActor> _producer;

        private readonly Receive _receiveMiddleware;
        private readonly Sender _senderMiddleware;
        private readonly ISupervisorStrategy _supervisorStrategy;
        private FastSet<PID> _children;
        private object _message;

        //TODO: I would like to extract these two as optional components in the future
        //for ReceiveTimeout we could have an object with the SetReceiveTimeout
        //and simply let this object subscribe to actor messages so it knows when to reset the timer
        private Timer _receiveTimeoutTimer;

        private RestartStatistics _restartStatistics;

        //for Stashing, there could be an object with the Stash, Unstash and UnstashAll
        //the main concern for this would be how to make the stash survive between actor restarts
        //if it is injected as a dependency, that would work fine
        private Stack<object> _stash;

        private ContextState _state;
        private FastSet<PID> _watchers;

        public ActorContext(Func<IActor> producer, ISupervisorStrategy supervisorStrategy, Receive receiveMiddleware, Sender senderMiddleware, PID parent)
        {
            _producer = producer;
            _supervisorStrategy = supervisorStrategy;
            _receiveMiddleware = receiveMiddleware;
            _senderMiddleware = senderMiddleware;

            //Parents are implicitly watching the child
            //The parent is not part of the Watchers set
            Parent = parent;

            IncarnateActor();
        }

        private static ILogger Logger { get; } = Log.CreateLogger<ActorContext>();

        public IReadOnlyCollection<PID> Children => _children?.ToList() ?? EmptyChildren;

        public IActor Actor { get; private set; }
        public PID Parent { get; }
        public PID Self { get; set; }

        public object Message => MessageEnvelope.UnwrapMessage(_message);

        public PID Sender => MessageEnvelope.UnwrapSender(_message);

        public MessageHeader Headers => MessageEnvelope.UnwrapHeader(_message);

        public TimeSpan ReceiveTimeout { get; private set; }

        public void Stash()
        {
            if (_stash == null)
            {
                _stash = new Stack<object>();
            }
            _stash.Push(Message);
        }

        public void Respond(object message)
        {
            Send(Sender, message);
        }

        public PID Spawn(Props props)
        {
            var id = ProcessRegistry.Instance.NextId();
            return SpawnNamed(props, id);
        }

        public PID SpawnPrefix(Props props, string prefix)
        {
            var name = prefix + ProcessRegistry.Instance.NextId();
            return SpawnNamed(props, name);
        }

        public PID SpawnNamed(Props props, string name)
        {
            if (props.GuardianStrategy != null)
            {
                throw new ArgumentException("Props used to spawn child cannot have GuardianStrategy.");
            }

            var pid = props.Spawn($"{Self.Id}/{name}", Self);
            if (_children == null)
            {
                _children = new FastSet<PID>();
            }
            _children.Add(pid);

            return pid;
        }

        public void Watch(PID pid)
        {
            pid.SendSystemMessage(new Watch(Self));
        }

        public void Unwatch(PID pid)
        {
            pid.SendSystemMessage(new Unwatch(Self));
        }

        public void SetReceiveTimeout(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "Duration must be greater than zero");
            }

            if (duration == ReceiveTimeout)
            {
                return;
            }

            StopReceiveTimeout();
            ReceiveTimeout = duration;

            if (_receiveTimeoutTimer == null)
            {
                _receiveTimeoutTimer = new Timer(ReceiveTimeoutCallback, null, ReceiveTimeout, ReceiveTimeout);
            }
            else
            {
                ResetReceiveTimeout();
            }
        }

        public void CancelReceiveTimeout()
        {
            if (_receiveTimeoutTimer == null)
            {
                return;
            }
            StopReceiveTimeout();
            _receiveTimeoutTimer = null;
            ReceiveTimeout = TimeSpan.Zero;
        }

        public Task ReceiveAsync(object message) => ProcessMessageAsync(message);

        public void Send(PID target, object message) => SendUserMessage(target, message);

        public void Forward(PID target)
        {
            if (_message is SystemMessage)
            {
                //SystemMessage cannot be forwarded
                Logger.LogWarning("SystemMessage cannot be forwarded. {0}", _message);
                return;
            }
            SendUserMessage(target, _message);
        }

        public void Request(PID target, object message)
        {
            var messageEnvelope = new MessageEnvelope(message, Self, null);
            SendUserMessage(target, messageEnvelope);
        }

        public Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout)
            => RequestAsync(target, message, new FutureProcess<T>(timeout));

        public Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
            => RequestAsync(target, message, new FutureProcess<T>(cancellationToken));

        public Task<T> RequestAsync<T>(PID target, object message)
            => RequestAsync(target, message, new FutureProcess<T>());

        public void ReenterAfter<T>(Task<T> target, Func<Task<T>, Task> action)
        {
            var msg = _message;
            var cont = new Continuation(() => action(target), msg);

            target.ContinueWith(t => { Self.SendSystemMessage(cont); });
        }

        public void ReenterAfter(Task target, Action action)
        {
            var msg = _message;
            var cont = new Continuation(() =>
            {
                action();
                return Done;
            }, msg);

            target.ContinueWith(t => { Self.SendSystemMessage(cont); });
        }


        public void EscalateFailure(Exception reason, PID who)
        {
            if (_restartStatistics == null)
            {
                _restartStatistics = new RestartStatistics(0, null);
            }
            var failure = new Failure(Self, reason, _restartStatistics);
            Self.SendSystemMessage(SuspendMailbox.Instance);
            if (Parent == null)
            {
                HandleRootFailure(failure);
            }
            else
            {
                Parent.SendSystemMessage(failure);
            }
        }

        public void RestartChildren(Exception reason, params PID[] pids) => pids.SendSystemNessage(new Restart(reason));

        public void StopChildren(params PID[] pids) => pids.SendSystemNessage(Stop.Instance);

        public void ResumeChildren(params PID[] pids) => pids.SendSystemNessage(ResumeMailbox.Instance);

        public Task InvokeSystemMessageAsync(object msg)
        {
            try
            {
                switch (msg)
                {
                    case Started s:
                        return InvokeUserMessageAsync(s);
                    case Stop _:
                        return InitiateStopAsync();
                    case Terminated t:
                        return HandleTerminatedAsync(t);
                    case Watch w:
                        HandleWatch(w);
                        return Done;
                    case Unwatch uw:
                        HandleUnwatch(uw);
                        return Done;
                    case Failure f:
                        HandleFailure(f);
                        return Done;
                    case Restart _:
                        return HandleRestartAsync();
                    case SuspendMailbox _:
                        return Done;
                    case ResumeMailbox _:
                        return Done;
                    case Continuation cont:
                        _message = cont.Message;
                        return cont.Action();
                    default:
                        Logger.LogWarning("Unknown system message {0}", msg);
                        return Done;
                }
            }
            catch (Exception x)
            {
                Logger.LogError("Error handling SystemMessage {0}", x);
                throw;
            }
        }

        public Task InvokeUserMessageAsync(object msg)
        {
            if (_state == ContextState.Stopped)
            {
                //already stopped
                Logger.LogError("Actor already stopped, ignore user message {0}", msg);
                return Done;
            }

            var influenceTimeout = true;
            if (ReceiveTimeout > TimeSpan.Zero)
            {
                var notInfluenceTimeout = msg is INotInfluenceReceiveTimeout;
                influenceTimeout = !notInfluenceTimeout;
                if (influenceTimeout)
                {
                    StopReceiveTimeout();
                }
            }

            var res = ProcessMessageAsync(msg);

            if (ReceiveTimeout != TimeSpan.Zero && influenceTimeout)
            {
                //special handle non completed tasks that need to reset ReceiveTimout
                if (!res.IsCompleted)
                {
                    return res.ContinueWith(_ => ResetReceiveTimeout());
                }

                ResetReceiveTimeout();
            }
            return res;
        }

        public void EscalateFailure(Exception reason, object message) => EscalateFailure(reason, Self);

        internal static Task DefaultReceive(IContext context)
        {
            var c = (ActorContext)context;
            if (c.Message is PoisonPill)
            {
                c.Self.Stop();
                return Done;
            }
            return c.Actor.ReceiveAsync(context);
        }

        internal static Task DefaultSender(ISenderContext context, PID target, MessageEnvelope envelope)
        {
            target.Ref.SendUserMessage(target, envelope);
            return Done;
        }

        private Task ProcessMessageAsync(object msg)
        {
            _message = msg;
            if (_receiveMiddleware != null)
            {
                return _receiveMiddleware(this);
            }
            return DefaultReceive(this);
        }

        private Task<T> RequestAsync<T>(PID target, object message, FutureProcess<T> future)
        {
            var messageEnvelope = new MessageEnvelope(message, future.Pid, null);
            SendUserMessage(target, messageEnvelope);
            return future.Task;
        }

        private void SendUserMessage(PID target, object message)
        {
            if (_senderMiddleware != null)
            {
                if (message is MessageEnvelope messageEnvelope)
                {
                    //Request based middleware
                    _senderMiddleware(this, target, messageEnvelope);
                }
                else
                {
                    //tell based middleware
                    _senderMiddleware(this, target, new MessageEnvelope(message, null, null));
                }
            }
            else
            {
                //Default path
                target.SendUserMessage(message);
            }
        }

        private void IncarnateActor()
        {
            _state = ContextState.Alive;
            Actor = _producer();
        }

        private async Task HandleRestartAsync()
        {
            _state = ContextState.Restarting;
            CancelReceiveTimeout();
            await InvokeUserMessageAsync(Restarting.Instance);
            await StopAllChildren();
        }

        private void HandleUnwatch(Unwatch uw) => _watchers?.Remove(uw.Watcher);

        private void HandleWatch(Watch w)
        {
            if (_state >= ContextState.Stopping)
            {
                w.Watcher.SendSystemMessage(Terminated.From(Self));
            }
            else
            {
                if (_watchers == null)
                {
                    _watchers = new FastSet<PID>();
                }
                _watchers.Add(w.Watcher);
            }
        }

        private void HandleFailure(Failure msg)
        {
            switch (Actor)
            {
                case ISupervisorStrategy supervisor:
                    supervisor.HandleFailure(this, msg.Who, msg.RestartStatistics, msg.Reason);
                    break;
                default:
                    _supervisorStrategy.HandleFailure(this, msg.Who, msg.RestartStatistics, msg.Reason);
                    break;
            }
        }

        private async Task HandleTerminatedAsync(Terminated msg)
        {
            _children?.Remove(msg.Who);
            await InvokeUserMessageAsync(msg);
            if (_state == ContextState.Stopping || _state == ContextState.Restarting)
            {
                await TryRestartOrStopAsync();
            }
        }

        private void HandleRootFailure(Failure failure)
        {
            Supervision.DefaultStrategy.HandleFailure(this, failure.Who, failure.RestartStatistics, failure.Reason);
        }
        
        //Initiate stopping, not final
        private async Task InitiateStopAsync()
        {
            if (_state >= ContextState.Stopping)
            {
                //already stopping or stopped
                return;
            }

            _state = ContextState.Stopping;
            CancelReceiveTimeout();
            //this is intentional
            await InvokeUserMessageAsync(Stopping.Instance);
            await StopAllChildren();
        }

        private async Task StopAllChildren()
        {
            _children?.Stop();
            await TryRestartOrStopAsync();
        }

        //intermediate stopping stage, waiting for children to stop
        private Task TryRestartOrStopAsync()
        {
            if (_children?.Count > 0)
            {
                return Done;
            }

            switch (_state)
            {
                case ContextState.Restarting:
                    return RestartAsync();
                case ContextState.Stopping:
                    return FinalizeStopAsync();
                default: return Done;
            }
        }

        //Last and final termination step
        private async Task FinalizeStopAsync()
        {
            ProcessRegistry.Instance.Remove(Self);
            //This is intentional
            await InvokeUserMessageAsync(Stopped.Instance);

            DisposeActorIfDisposable();

            //Notify watchers
            _watchers?.SendSystemNessage(Terminated.From(Self));

            //Notify parent
            Parent?.SendSystemMessage(Terminated.From(Self));

            _state = ContextState.Stopped;
        }

        private async Task RestartAsync()
        {
            DisposeActorIfDisposable();
            IncarnateActor();
            Self.SendSystemMessage(ResumeMailbox.Instance);

            await InvokeUserMessageAsync(Started.Instance);
            if (_stash != null)
            {
                while (_stash.Any())
                {
                    var msg = _stash.Pop();
                    await InvokeUserMessageAsync(msg);
                }
            }
        }

        private void DisposeActorIfDisposable()
        {
            if (Actor is IDisposable disposableActor)
            {
                disposableActor.Dispose();
            }
        }

        private void ResetReceiveTimeout() => _receiveTimeoutTimer?.Change(ReceiveTimeout, ReceiveTimeout);

        private void StopReceiveTimeout() => _receiveTimeoutTimer?.Change(-1, -1);

        private void ReceiveTimeoutCallback(object state)
        {
            if (_receiveTimeoutTimer == null)
            {
                return;
            }
            CancelReceiveTimeout();
            Send(Self,Proto.ReceiveTimeout.Instance);
        }
    }
}