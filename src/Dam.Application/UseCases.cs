using System.Text.RegularExpressions;
using Dam.Domain;
using Mdm.Core;

namespace Dam.Application;

public record UploadMediaCommand(IReadOnlyCollection<string> FileNames);
public record AutoLinkMediaCommand(IReadOnlyCollection<string> FileNames);

public interface IUploadMediaBatch
{
    Task ExecuteAsync(UploadMediaCommand command, CancellationToken ct = default);
}

public interface IAutoLinkMediaByCodes
{
    Task ExecuteAsync(AutoLinkMediaCommand command, CancellationToken ct = default);
}

public class UploadMediaBatchInteractor : IUploadMediaBatch
{
    private readonly IMediaRepository _repo;
    private readonly IEnumerable<IMediaHandler> _handlers;
    private readonly IEventPublisher _events;

    public UploadMediaBatchInteractor(IMediaRepository repo, IEnumerable<IMediaHandler> handlers, IEventPublisher events)
    {
        _repo = repo; _handlers = handlers; _events = events;
    }

    public async Task ExecuteAsync(UploadMediaCommand command, CancellationToken ct = default)
    {
        foreach (var file in command.FileNames)
        {
            var (ean, sku, fmt) = Parse(file);
            var media = new Media(Guid.NewGuid(), file, ean, sku, fmt);
            await _repo.AddAsync(media, ct);

            foreach (var h in _handlers.Where(h => h.CanHandle(fmt)))
                await h.ProcessAsync(media, ct);

            await _events.PublishAsync(new MediaUploaded(file, ean, sku), ct);
        }
    }

    private static (string ean, string sku, string format) Parse(string file)
    {
        // EAN12345_SKU56789_front.jpg
        var name = Path.GetFileName(file);
        var match = Regex.Match(name, @"EAN(?<ean>\w+)_SKU(?<sku>\w+).*?\.(?<ext>\w+)$", RegexOptions.IgnoreCase);
        if (!match.Success) throw new InvalidOperationException($"Bad filename: {file}");
        return (match.Groups["ean"].Value, match.Groups["sku"].Value, match.Groups["ext"].Value.ToLowerInvariant());
    }
}

public class AutoLinkMediaByCodesInteractor : IAutoLinkMediaByCodes, IEventSubscriber, IMediaLinkService
{
    private readonly IMediaRepository _repo;
    public AutoLinkMediaByCodesInteractor(IMediaRepository repo) { _repo = repo; }

    public async Task ExecuteAsync(AutoLinkMediaCommand command, CancellationToken ct = default)
    {
        foreach (var f in command.FileNames)
        {
            var name = Path.GetFileName(f);
            var match = Regex.Match(name, @"EAN(?<ean>\w+)_SKU(?<sku>\w+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var media = await _repo.GetByFileNameAsync(name, ct);
                if (media is not null)
                {
                    await _repo.AddLinkAsync(new MediaLink(Guid.NewGuid(), match.Groups["ean"].Value, match.Groups["sku"].Value, media.Id), ct);
                }
            }
        }
    }

    // IMediaLinkService
    public async Task LinkAsync(string ean, string sku, string fileName, CancellationToken ct = default)
    {
        var media = await _repo.GetByFileNameAsync(fileName, ct);
        if (media is null) return;
        await _repo.AddLinkAsync(new MediaLink(Guid.NewGuid(), ean, sku, media.Id), ct);
    }

    // IEventSubscriber
    public async Task HandleAsync(Mdm.Core.IEvent @event, CancellationToken ct = default)
    {
        if (@event is Mdm.Core.MediaUploaded m)
        {
            await LinkAsync(m.Ean, m.Sku, m.FileName, ct);
        }
    }
}

// ---- Consultation des liens ----
public record GetMediaLinksQuery(string Ean, string? Sku);

public interface IGetMediaLinksForProduct
{
    Task<IReadOnlyCollection<MediaLink>> ExecuteAsync(GetMediaLinksQuery query, CancellationToken ct = default);
}

public class GetMediaLinksForProductInteractor : IGetMediaLinksForProduct
{
    private readonly IMediaRepository _repo;
    public GetMediaLinksForProductInteractor(IMediaRepository repo) { _repo = repo; }

    public async Task<IReadOnlyCollection<MediaLink>> ExecuteAsync(GetMediaLinksQuery query, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(query.Sku))
            return await _repo.GetLinksByEanAndSkuAsync(query.Ean, query.Sku!, ct);
        return await _repo.GetLinksByEanAsync(query.Ean, ct);
    }
}

// ---- Listing médias ----
public record MediaQuery(string? Format);

public interface IListMedia
{
    Task<IReadOnlyCollection<Media>> ExecuteAsync(MediaQuery query, CancellationToken ct = default);
}

public class ListMediaInteractor : IListMedia
{
    private readonly IMediaRepository _repo;
    public ListMediaInteractor(IMediaRepository repo) { _repo = repo; }

    public async Task<IReadOnlyCollection<Media>> ExecuteAsync(MediaQuery query, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(query.Format))
            return await _repo.ListByFormatAsync(query.Format!, ct);
        return await _repo.ListAsync(ct);
    }
}
