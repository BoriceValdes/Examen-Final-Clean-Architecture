using Pim.Domain;

namespace Pim.Infrastructure.InMemory;

public class InMemoryProductRepository : IProductRepository
{
    private readonly Dictionary<string, Product> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task AddAsync(Product product, CancellationToken ct = default)
    {
        _store[product.Ean] = product;
        return Task.CompletedTask;
    }

    public Task<Product?> GetByEanAsync(string ean, CancellationToken ct = default)
        => Task.FromResult(_store.TryGetValue(ean, out var p) ? p : null);

    public Task<bool> ExistsAsync(string ean, CancellationToken ct = default)
        => Task.FromResult(_store.ContainsKey(ean));

    public Task<IReadOnlyCollection<Product>> ListAsync(CancellationToken ct = default)
        => Task.FromResult((IReadOnlyCollection<Product>)_store.Values.ToList());

    public Task<IReadOnlyCollection<Product>> ListByTypologyAsync(string typologyCode, CancellationToken ct = default)
        => Task.FromResult((IReadOnlyCollection<Product>)_store.Values
            .Where(p => string.Equals(p.TypologyCode, typologyCode, StringComparison.OrdinalIgnoreCase)).ToList());
}

public class InMemoryTypologyRepository : ITypologyRepository
{
    private readonly Dictionary<string, Typology> _store;
    public InMemoryTypologyRepository(IEnumerable<Typology> seeds)
    {
        _store = seeds.ToDictionary(t => t.Code, t => t, StringComparer.OrdinalIgnoreCase);
    }

    public Task<Typology?> GetByCodeAsync(string code, CancellationToken ct = default)
        => Task.FromResult(_store.TryGetValue(code, out var t) ? t : null);
}
