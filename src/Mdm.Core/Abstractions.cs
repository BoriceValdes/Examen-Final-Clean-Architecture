
namespace Mdm.Core;

public interface IEvent { }

public record MediaUploaded(string FileName, string Ean, string Sku) : IEvent;
public record ProductCreated(string Ean) : IEvent;

public interface IEventPublisher
{
    Task PublishAsync(IEvent @event, CancellationToken ct = default);
}

public interface IEventSubscriber
{
    Task HandleAsync(IEvent @event, CancellationToken ct = default);
}

public interface IMdmOrchestrator
{
    Task OnEventAsync(IEvent @event, CancellationToken ct = default);
}

// PIM-facing ports
public interface IProductService
{
    Task<bool> ProductExistsAsync(string ean, CancellationToken ct = default);
}

// DAM-facing ports
public interface IMediaLinkService
{
    Task LinkAsync(string ean, string sku, string fileName, CancellationToken ct = default);
}
