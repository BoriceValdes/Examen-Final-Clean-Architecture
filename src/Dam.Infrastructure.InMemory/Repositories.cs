using Dam.Domain;

namespace Dam.Infrastructure.InMemory;

public class InMemoryMediaRepository : IMediaRepository
{
    private readonly Dictionary<string, Media> _byFile = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MediaLink> _links = new();

    public Task AddAsync(Media media, CancellationToken ct = default)
    {
        _byFile[media.FileName] = media;
        return Task.CompletedTask;
    }

    public Task<Media?> GetByFileNameAsync(string fileName, CancellationToken ct = default)
        => Task.FromResult(_byFile.TryGetValue(Path.GetFileName(fileName), out var m) ? m : null);

    public Task AddLinkAsync(MediaLink link, CancellationToken ct = default)
    {
        _links.Add(link);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<MediaLink>> GetLinksByEanAsync(string ean, CancellationToken ct = default)
    {
        var res = _links.Where(l => string.Equals(l.Ean, ean, StringComparison.OrdinalIgnoreCase)).ToList();
        return Task.FromResult((IReadOnlyCollection<MediaLink>)res);
    }

    public Task<IReadOnlyCollection<MediaLink>> GetLinksByEanAndSkuAsync(string ean, string sku, CancellationToken ct = default)
    {
        var res = _links.Where(l =>
            string.Equals(l.Ean, ean, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(l.Sku, sku, StringComparison.OrdinalIgnoreCase)).ToList();
        return Task.FromResult((IReadOnlyCollection<MediaLink>)res);
    }

    public Task<IReadOnlyCollection<Media>> ListAsync(CancellationToken ct = default)
        => Task.FromResult((IReadOnlyCollection<Media>)_byFile.Values.ToList());

    public Task<IReadOnlyCollection<Media>> ListByFormatAsync(string format, CancellationToken ct = default)
        => Task.FromResult((IReadOnlyCollection<Media>)_byFile.Values
            .Where(m => string.Equals(m.Format, format, StringComparison.OrdinalIgnoreCase)).ToList());
}

// Handlers extensibles
public class JpegMediaHandler : IMediaHandler
{
    public bool CanHandle(string format)
        => format.Equals("jpg", StringComparison.OrdinalIgnoreCase) ||
           format.Equals("jpeg", StringComparison.OrdinalIgnoreCase);

    public Task ProcessAsync(Media media, CancellationToken ct = default)
        => Task.CompletedTask; // e.g. thumbnails
}

public class PdfMediaHandler : IMediaHandler
{
    public bool CanHandle(string format)
        => format.Equals("pdf", StringComparison.OrdinalIgnoreCase);

    public Task ProcessAsync(Media media, CancellationToken ct = default)
        => Task.CompletedTask; // e.g. extract pages
}
