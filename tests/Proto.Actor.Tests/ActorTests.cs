using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.TestFixtures;
using Xunit;
using static Proto.TestFixtures.Receivers;

namespace Proto.Tests
{
    public class ActorTests
    {
        public static PID SpawnActorFromFunc(Receive receive) => Actor.Spawn(Actor.FromFunc(receive));


        [Fact]
        public async Task RequestActorAsync()
        {
            PID pid = SpawnActorFromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }
                return Actor.Done;
            });

            var reply = await pid.RequestAsync<object>("hello");

            reply.Should().Be("hey");
        }

        [Fact]
        public void RequestActorAsync_should_raise_TimeoutException_when_timeout_is_reached()
        {
            PID pid = SpawnActorFromFunc(EmptyReceive);

            Func<Task> action = async () => await pid.RequestAsync<object>("", TimeSpan.FromMilliseconds(20));

            action.ShouldThrow<TimeoutException>()
                .And.Message.Should().Be("Request didn't receive any Response within the expected time.");
        }

        [Fact]
        public async Task RequestActorAsync_should_not_raise_TimeoutException_when_result_is_first()
        {
            PID pid = SpawnActorFromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }
                return Actor.Done;
            });

            var reply = await pid.RequestAsync<object>("hello", TimeSpan.FromMilliseconds(100));

            reply.Should().Be("hey");
        }

        [Fact]
        public void ActorLifeCycle()
        {
            var messages = new Queue<object>();

            var pid = Actor.Spawn(
                Actor
                    .FromFunc(ctx =>
                    {
                        messages.Enqueue(ctx.Message);
                        return Actor.Done;
                    })
                    .WithMailbox(() => new TestMailbox())
                );

            pid.Tell("hello");
            pid.Stop();

            messages.ShouldBeEquivalentTo(new object[]
            {
                Started.Instance,
                "hello",
                Stopping.Instance,
                Stopped.Instance
            });
        }
    }
}
