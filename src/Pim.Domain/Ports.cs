
namespace Pim.Domain;

public interface IProductRepository
{
    Task AddAsync(Product product, CancellationToken ct = default);
    Task<Product?> GetByEanAsync(string ean, CancellationToken ct = default);
    Task<bool> ExistsAsync(string ean, CancellationToken ct = default);
}

public interface ITypologyRepository
{
    Task<Typology?> GetByCodeAsync(string code, CancellationToken ct = default);
}
