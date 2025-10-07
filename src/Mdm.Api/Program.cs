using Microsoft.OpenApi.Models;
using System.Text;
using Dam.Application;
using Dam.Domain;
using Dam.Infrastructure.InMemory;
using Mdm.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Pim.Application;                // Use cases & records PIM
using Pim.Domain;
using Pim.Infrastructure.InMemory;

var builder = WebApplication.CreateBuilder(args);

// 🔐 JWT – secret 32+ bytes impératif pour HS256
var jwtSecret = builder.Configuration["Jwt:Secret"]
                ?? "a-string-secret-at-least-256-bits-long"; // 64+ chars
if (Encoding.UTF8.GetBytes(jwtSecret).Length < 32)
{
    throw new InvalidOperationException("Jwt:Secret must be at least 32 bytes for HS256.");
}
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

// AuthN
builder.Services
    .AddAuthentication(options =>
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
            ValidateIssuerSigningKey = false,
            IssuerSigningKey = key
        };
    });

// AuthZ : 🔒 FallbackPolicy => toutes les routes exigent un utilisateur authentifié
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();

    // Exemples si tu veux des policies nommées :
    // options.AddPolicy("AdminOnly", p => p.RequireRole("admin"));
});


// ----------------- DI / Modules -----------------

// Event bus
builder.Services.AddSingleton<IEventSubscriber, AutoLinkMediaByCodesInteractor>();
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
builder.Services.AddTransient<IGetProductByEan, GetProductByEanInteractor>();
builder.Services.AddTransient<IListProducts, ListProductsInteractor>();

// DAM
builder.Services.AddSingleton<IMediaRepository, InMemoryMediaRepository>();
builder.Services.AddSingleton<IMediaHandler, JpegMediaHandler>();
builder.Services.AddSingleton<IMediaHandler, PdfMediaHandler>();
builder.Services.AddTransient<IUploadMediaBatch, UploadMediaBatchInteractor>();
builder.Services.AddTransient<IAutoLinkMediaByCodes, AutoLinkMediaByCodesInteractor>();
builder.Services.AddTransient<IGetMediaLinksForProduct, GetMediaLinksForProductInteractor>();
builder.Services.AddTransient<IListMedia, ListMediaInteractor>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MDM API", Version = "v1" });

    // --- Security: Bearer JWT correctement référencé ---
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter only the JWT *without* the 'Bearer ' prefix.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer" // << ID de la définition
        }
    };

    // Définition "Bearer"
    c.AddSecurityDefinition("Bearer", securityScheme);

    // Requirement global: toutes les opérations portent ce scheme
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer" // << DOIT matcher l'ID de la définition
                }
            },
            Array.Empty<string>()
        }
    });
});


var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();


// =================== ENDPOINTS ===================

// -------- AUTH demo (publique) --------
app.MapGet("/auth/demo-token", (string? role) =>
{
    // Génère un JWT (role: admin par défaut). Tu peux tester un 403 en passant ?role=user
    role = string.IsNullOrWhiteSpace(role) ? "admin" : role.Trim().ToLowerInvariant();
    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new List<System.Security.Claims.Claim>
    {
        new(System.Security.Claims.ClaimTypes.Role, role)
    };

    var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
        claims: claims,
        expires: DateTime.UtcNow.AddHours(1),
        signingCredentials: creds
    );
    return Results.Ok(new { token = handler.WriteToken(token), role });
})
.AllowAnonymous(); // 👈 public (ne nécessite pas de token)


// -------- PIM --------

// Create product (admin only)
app.MapPost("/api/pim/products", async (ICreateProduct uc, CreateProductDto dto, CancellationToken ct) =>
{
    await uc.ExecuteAsync(new CreateProductCommand(dto.Ean, dto.Name, dto.TypologyCode, dto.Attributes), ct);
    return Results.Created($"/api/pim/products/{dto.Ean}", new { dto.Ean });
});
    //.RequireAuthorization(policy => policy.RequireRole("admin"));

// List products (auth requis, n'importe quel rôle)
app.MapGet("/api/pim/products", async (IListProducts q, string? typology, CancellationToken ct) =>
{
    var res = await q.ExecuteAsync(new ProductsQuery(typology), ct);
    return Results.Ok(res.Select(p => new { p.Ean, p.Name, p.TypologyCode, Attributes = p.Attributes }));
}); // 👈 aucune RequireAuthorization => FallbackPolicy s’applique (auth requise)

// Get product by EAN (auth requis)
app.MapGet("/api/pim/products/{ean}", async (IGetProductByEan q, string ean, CancellationToken ct) =>
{
    var p = await q.ExecuteAsync(ean, ct);
    return p is null ? Results.NotFound() : Results.Ok(new { p.Ean, p.Name, p.TypologyCode, Attributes = p.Attributes });
}); // 👈 auth requise via FallbackPolicy

// Update attributes (admin only)
app.MapPut("/api/pim/products/{ean}/attributes", async (IUpdateProductAttributes uc, string ean, Dictionary<string, object?> attributes, CancellationToken ct) =>
{
    await uc.ExecuteAsync(new UpdateProductAttributesCommand(ean, attributes), ct);
    return Results.NoContent();
});
//.RequireAuthorization(policy => policy.RequireRole("admin")); // 👈 admin requis


// -------- DAM --------

// Upload media (auth requis)
app.MapPost("/api/dam/media/upload", async (IUploadMediaBatch uc, UploadMediaDto dto, CancellationToken ct) =>
{
    await uc.ExecuteAsync(new UploadMediaCommand(dto.FileNames), ct);
    return Results.Accepted();
}); // 👈 auth requise via FallbackPolicy

// List media (auth requis)
app.MapGet("/api/dam/media", async (IListMedia q, string? format, CancellationToken ct) =>
{
    var res = await q.ExecuteAsync(new MediaQuery(format), ct);
    return Results.Ok(res);
}); // 👈 auth requise via FallbackPolicy

// Get media by filename (auth requis)
app.MapGet("/api/dam/media/{fileName}", async (IMediaRepository repo, string fileName, CancellationToken ct) =>
{
    var m = await repo.GetByFileNameAsync(fileName, ct);
    return m is null ? Results.NotFound() : Results.Ok(m);
}); // 👈 auth requise via FallbackPolicy

// Links by EAN (auth requis)
app.MapGet("/api/dam/links/{ean}", async (IGetMediaLinksForProduct q, string ean, string? sku, CancellationToken ct) =>
{
    var links = await q.ExecuteAsync(new GetMediaLinksQuery(ean, sku), ct);
    return Results.Ok(links);
}); // 👈 auth requise via FallbackPolicy


app.Run();


// --- DTOs locaux API ---
public record CreateProductDto(string Ean, string Name, string TypologyCode, Dictionary<string, object?> Attributes);
public record UploadMediaDto(IReadOnlyCollection<string> FileNames);
