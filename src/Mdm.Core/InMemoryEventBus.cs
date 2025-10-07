
using System.Collections.Concurrent;

namespace Mdm.Core;

public sealed class InMemoryEventBus : IEventPublisher
{
    private readonly IEnumerable<IEventSubscriber> _subscribers;
    public InMemoryEventBus(IEnumerable<IEventSubscriber> subscribers)
    {
        _subscribers = subscribers;
    }

    public async Task PublishAsync(IEvent @event, CancellationToken ct = default)
    {
        var tasks = _subscribers.Select(s => s.HandleAsync(@event, ct));
        await Task.WhenAll(tasks);
    }
}
