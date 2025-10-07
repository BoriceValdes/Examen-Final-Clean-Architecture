
using Pim.Domain;
using Mdm.Core;

namespace Pim.Application;

public record CreateProductCommand(string Ean, string Name, string TypologyCode, IDictionary<string, object?> Attributes);
public record UpdateProductAttributesCommand(string Ean, IDictionary<string, object?> Attributes);

public interface ICreateProduct
{
    Task ExecuteAsync(CreateProductCommand command, CancellationToken ct = default);
}

public interface IUpdateProductAttributes
{
    Task ExecuteAsync(UpdateProductAttributesCommand command, CancellationToken ct = default);
}

public class CreateProductInteractor : ICreateProduct
{
    private readonly IProductRepository _products;
    private readonly ITypologyRepository _typologies;
    private readonly IEventPublisher _events;

    public CreateProductInteractor(IProductRepository products, ITypologyRepository typologies, IEventPublisher events)
    {
        _products = products;
        _typologies = typologies;
        _events = events;
    }

    public async Task ExecuteAsync(CreateProductCommand command, CancellationToken ct = default)
    {
        if (await _products.ExistsAsync(command.Ean, ct))
            throw new InvalidOperationException("Product already exists");

        var typology = await _typologies.GetByCodeAsync(command.TypologyCode, ct) 
                       ?? throw new InvalidOperationException("Unknown typology");

        var product = new Product(command.Ean, command.Name, typology.Code);
        product.UpdateAttributes(typology, command.Attributes);

        await _products.AddAsync(product, ct);
        await _events.PublishAsync(new ProductCreated(product.Ean), ct);
    }
}

public class UpdateProductAttributesInteractor : IUpdateProductAttributes
{
    private readonly IProductRepository _products;
    private readonly ITypologyRepository _typologies;

    public UpdateProductAttributesInteractor(IProductRepository products, ITypologyRepository typologies)
    {
        _products = products;
        _typologies = typologies;
    }

    public async Task ExecuteAsync(UpdateProductAttributesCommand command, CancellationToken ct = default)
    {
        var product = await _products.GetByEanAsync(command.Ean, ct) 
                      ?? throw new InvalidOperationException("Product not found");

        var typology = await _typologies.GetByCodeAsync(product.TypologyCode, ct)
                       ?? throw new InvalidOperationException("Typology not found");

        product.UpdateAttributes(typology, command.Attributes);
    }
}


public interface IGetProductByEan
{
    Task<Product?> ExecuteAsync(string ean, CancellationToken ct = default);
}

public class GetProductByEanInteractor : IGetProductByEan
{
    private readonly IProductRepository _products;
    public GetProductByEanInteractor(IProductRepository products) { _products = products; }
    public Task<Product?> ExecuteAsync(string ean, CancellationToken ct = default) => _products.GetByEanAsync(ean, ct);
}


public record ProductsQuery(string? TypologyCode);

public interface IListProducts
{
    Task<IReadOnlyCollection<Product>> ExecuteAsync(ProductsQuery query, CancellationToken ct = default);
}

public class ListProductsInteractor : IListProducts
{
    private readonly IProductRepository _products;
    public ListProductsInteractor(IProductRepository products) { _products = products; }

    public async Task<IReadOnlyCollection<Product>> ExecuteAsync(ProductsQuery query, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(query.TypologyCode))
            return await _products.ListByTypologyAsync(query.TypologyCode!, ct);
        return await _products.ListAsync(ct);
    }
}
