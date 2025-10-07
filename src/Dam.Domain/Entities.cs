namespace Dam.Domain;

public record Media(Guid Id, string FileName, string Ean, string Sku, string Format);

public record MediaLink(Guid Id, string Ean, string Sku, Guid MediaId);

public interface IMediaRepository
{
    Task AddAsync(Media media, CancellationToken ct = default);
    Task<Media?> GetByFileNameAsync(string fileName, CancellationToken ct = default);
    Task AddLinkAsync(MediaLink link, CancellationToken ct = default);

    // Consultation des liens
    Task<IReadOnlyCollection<MediaLink>> GetLinksByEanAsync(string ean, CancellationToken ct = default);
    Task<IReadOnlyCollection<MediaLink>> GetLinksByEanAndSkuAsync(string ean, string sku, CancellationToken ct = default);

    // Listing / filtres
    Task<IReadOnlyCollection<Media>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyCollection<Media>> ListByFormatAsync(string format, CancellationToken ct = default);
}

public interface IMediaHandler
{
    bool CanHandle(string format);
    Task ProcessAsync(Media media, CancellationToken ct = default);
}
