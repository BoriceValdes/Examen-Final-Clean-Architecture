
using Pim.Application;
using Pim.Domain;
using Pim.Infrastructure.InMemory;
using Mdm.Core;
using Xunit;

public class DummyBus : IEventPublisher
{
    public List<IEvent> Events { get; } = new();
    public Task PublishAsync(IEvent @event, CancellationToken ct = default)
    {
        Events.Add(@event);
        return Task.CompletedTask;
    }
}

public class CreateProductTests
{
    [Fact]
    public async Task Creates_product_and_publishes_event()
    {
        // Arrange
        var products = new InMemoryProductRepository();
        var typologies = new InMemoryTypologyRepository(new []{
            new Typology("TEXTILE","Textile", new []{
                new FormField("size", FieldKind.Text, true),
                new FormField("price", FieldKind.Number, true)
            }),
        });
        var bus = new DummyBus();
        var uc = new CreateProductInteractor(products, typologies, bus);

        // Act
        await uc.ExecuteAsync(new CreateProductCommand("EAN1","T-Shirt","TEXTILE", new Dictionary<string, object?>{
            ["size"]="M",
            ["price"]=19.99
        }));

        // Assert
        var p = await products.GetByEanAsync("EAN1");
        Assert.NotNull(p);
        Assert.Equal("M", p!.Attributes["size"]);
        Assert.Contains(bus.Events, e => e is ProductCreated pc && pc.Ean=="EAN1");
    }
}
