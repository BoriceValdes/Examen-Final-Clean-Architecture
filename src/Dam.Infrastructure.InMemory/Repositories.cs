
using Dam.Domain;

namespace Dam.Infrastructure.InMemory;

public class InMemoryMediaRepository : IMediaRepository
{
    private readonly Dictionary<string, Media> _byFile = new();
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
}

// Example extensible handlers
public class JpegMediaHandler : IMediaHandler
{
    public bool CanHandle(string format) => format.Equals("jpg", StringComparison.OrdinalIgnoreCase) || format.Equals("jpeg", StringComparison.OrdinalIgnoreCase);
    public Task ProcessAsync(Media media, CancellationToken ct = default) => Task.CompletedTask; // thumbnail, etc.
}

public class PdfMediaHandler : IMediaHandler
{
    public bool CanHandle(string format) => format.Equals("pdf", StringComparison.OrdinalIgnoreCase);
    public Task ProcessAsync(Media media, CancellationToken ct = default) => Task.CompletedTask; // extract pages, etc.
}
