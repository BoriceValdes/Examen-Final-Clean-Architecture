
namespace Dam.Domain;

public record Media(Guid Id, string FileName, string Ean, string Sku, string Format);
public record MediaLink(Guid Id, string Ean, string Sku, Guid MediaId);

public interface IMediaRepository
{
    Task AddAsync(Media media, CancellationToken ct = default);
    Task<Media?> GetByFileNameAsync(string fileName, CancellationToken ct = default);
    Task AddLinkAsync(MediaLink link, CancellationToken ct = default);
}

public interface IMediaHandler
{
    bool CanHandle(string format);
    Task ProcessAsync(Media media, CancellationToken ct = default);
}
