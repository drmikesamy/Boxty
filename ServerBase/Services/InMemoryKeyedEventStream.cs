using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Boxty.ServerBase.Interfaces;

namespace Boxty.ServerBase.Services
{
    public class InMemoryKeyedEventStream<TKey, TEvent> : IKeyedEventStream<TKey, TEvent>
        where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, ConcurrentDictionary<Guid, Channel<TEvent>>> _subscriptions = new();

        public async IAsyncEnumerable<TEvent> Subscribe(TKey key, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var subscriptionId = Guid.NewGuid();
            var channel = Channel.CreateBounded<TEvent>(new BoundedChannelOptions(256)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });

            var perKeySubscriptions = _subscriptions.GetOrAdd(key, _ => new ConcurrentDictionary<Guid, Channel<TEvent>>());
            perKeySubscriptions[subscriptionId] = channel;

            try
            {
                await foreach (var @event in channel.Reader.ReadAllAsync(cancellationToken))
                {
                    yield return @event;
                }
            }
            finally
            {
                if (_subscriptions.TryGetValue(key, out var subscriptions))
                {
                    subscriptions.TryRemove(subscriptionId, out _);
                    if (subscriptions.IsEmpty)
                    {
                        _subscriptions.TryRemove(key, out _);
                    }
                }
            }
        }

        public ValueTask PublishAsync(TKey key, TEvent @event, CancellationToken cancellationToken = default)
        {
            if (!_subscriptions.TryGetValue(key, out var subscriptions))
            {
                return ValueTask.CompletedTask;
            }

            foreach (var subscription in subscriptions.Values)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                subscription.Writer.TryWrite(@event);
            }

            return ValueTask.CompletedTask;
        }
    }
}
