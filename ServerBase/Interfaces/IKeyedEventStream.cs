using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Boxty.ServerBase.Interfaces
{
    public interface IKeyedEventStream<TKey, TEvent>
        where TKey : notnull
    {
        IAsyncEnumerable<TEvent> Subscribe(TKey key, CancellationToken cancellationToken = default);
        ValueTask PublishAsync(TKey key, TEvent @event, CancellationToken cancellationToken = default);
    }
}
