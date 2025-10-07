
using System.Text;
using Dam.Application;
using Dam.Domain;
using Dam.Infrastructure.InMemory;
using Mdm.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Pim.Application;
using Pim.Domain;
using Pim.Infrastructure.InMemory;

var builder = WebApplication.CreateBuilder(args);

// JWT Demo (do NOT use this secret in production)
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "DEV-ONLY-SECRET-CHANGE-ME";
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key
    };
});

builder.Services.AddAuthorization();

// Core
builder.Services.AddSingleton<IEventSubscriber, AutoLinkMediaByCodesInteractor>(); // DAM reacts to MediaUploaded
builder.Services.AddSingleton<IEventPublisher, InMemoryEventBus>(sp => new InMemoryEventBus(sp.GetServices<IEventSubscriber>()));

// PIM
builder.Services.AddSingleton<IProductRepository, InMemoryProductRepository>();
builder.Services.AddSingleton<ITypologyRepository>(sp =>
{
    var typologies = new[] {
        new Typology("TEXTILE","Textile", new []{
            new FormField("size", FieldKind.Text, true),
            new FormField("color", FieldKind.Text, false),
            new FormField("material", FieldKind.Text, false),
            new FormField("price", FieldKind.Number, true)
        }),
        new Typology("ELECTRO","Electronic", new []{
            new FormField("cpu", FieldKind.Text, true),
            new FormField("ramGb", FieldKind.Number, true),
            new FormField("batteryMah", FieldKind.Number, false),
            new FormField("price", FieldKind.Number, true)
        }),
    };
    return new InMemoryTypologyRepository(typologies);
});
builder.Services.AddTransient<ICreateProduct, CreateProductInteractor>();
builder.Services.AddTransient<IUpdateProductAttributes, UpdateProductAttributesInteractor>();

// DAM
builder.Services.AddSingleton<IMediaRepository, InMemoryMediaRepository>();
builder.Services.AddSingleton<IMediaHandler, JpegMediaHandler>();
builder.Services.AddSingleton<IMediaHandler, PdfMediaHandler>();
builder.Services.AddTransient<IUploadMediaBatch, UploadMediaBatchInteractor>();
builder.Services.AddTransient<IAutoLinkMediaByCodes, AutoLinkMediaByCodesInteractor>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

// Minimal API

app.MapPost("/api/pim/products", async (ICreateProduct uc, CreateProductDto dto, CancellationToken ct) =>
{
    await uc.ExecuteAsync(new CreateProductCommand(dto.Ean, dto.Name, dto.TypologyCode, dto.Attributes), ct);
    return Results.Created($"/api/pim/products/{dto.Ean}", new { dto.Ean });
})
.RequireAuthorization(policy => policy.RequireRole("admin"));

app.MapPost("/api/dam/media/upload", async (IUploadMediaBatch uc, UploadMediaDto dto, CancellationToken ct) =>
{
    await uc.ExecuteAsync(new UploadMediaCommand(dto.FileNames), ct);
    return Results.Accepted();
});

// DTOs
public record CreateProductDto(string Ean, string Name, string TypologyCode, Dictionary<string, object?> Attributes);
public record UploadMediaDto(IReadOnlyCollection<string> FileNames);

app.Run();
