using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ZipZap.Api.Middleware;
using ZipZap.Api.Security;
using ZipZap.BuildingBlocks.DependencyInjection;
using ZipZap.BuildingBlocks.MultiTenancy;
using ZipZap.BuildingBlocks.Persistence;
using ZipZap.Modules.Identity;
using ZipZap.Modules.Identity.Api;
using ZipZap.Modules.Catalog;
using ZipZap.Modules.Catalog.Api;
using ZipZap.Modules.Ordering;
using ZipZap.Modules.Ordering.Api;
using ZipZap.Modules.Payments;
using ZipZap.Modules.Payments.Api;
using ZipZap.Modules.Delivery;
using ZipZap.Modules.Delivery.Api;
using ZipZap.Modules.Notifications;
using ZipZap.Modules.Notifications.Api;
using ZipZap.Modules.Integrations;
using ZipZap.Modules.Integrations.Api;

var builder = WebApplication.CreateBuilder(args);

// --- Wspólny rdzeń (event bus in-process, outbox dispatcher, kontekst najemcy) ---
builder.Services.AddBuildingBlocks();

// --- Moduły ---
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddOrderingModule(builder.Configuration);
builder.Services.AddPaymentsModule(builder.Configuration);
builder.Services.AddDeliveryModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddIntegrationsModule();

// --- Uwierzytelnianie / autoryzacja (JWT) ---
var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(jwt["SigningKey"] ?? throw new InvalidOperationException("Brak Jwt:SigningKey.")));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = "sub",
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", p => p.RequireRole("Admin"));
    options.AddPolicy("StoreEmployee", p => p.RequireRole("StoreEmployee", "Admin"));
    options.AddPolicy("Driver", p => p.RequireRole("Driver", "Admin"));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// CORS dla panelu Angular w dev (JWT w nagłówku — bez ciasteczek).
const string DevCorsPolicy = "dev-cors";
builder.Services.AddCors(o => o.AddPolicy(DevCorsPolicy, p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// --- Swagger (z obsługą Bearer) ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "ZipZap API", Version = "v1" });

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    options.AddSecurityDefinition("Bearer", scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
});

var app = builder.Build();

// --- Migracje modułów (wygoda dev/CI; błąd nie blokuje startu Swaggera) ---
await using (var scope = app.Services.CreateAsyncScope())
{
    foreach (var migrator in scope.ServiceProvider.GetServices<IModuleDbMigrator>())
    {
        try
        {
            await migrator.MigrateAsync();
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Migracja modułu {Module} nie powiodła się.", migrator.ModuleName);
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Bootstrap pierwszego administratora (tylko dev).
    await IdentityModule.SeedDevelopmentAdminAsync(
        app.Services,
        app.Configuration["Seed:AdminEmail"] ?? "admin@zipzap.local",
        app.Configuration["Seed:AdminPassword"] ?? "Admin123!");
}

if (app.Environment.IsDevelopment())
    app.UseCors(DevCorsPolicy);

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "ZipZap.Api",
    timeUtc = DateTime.UtcNow
}))
.WithName("Health")
.WithTags("System");

// --- Endpointy modułów ---
app.MapIdentityEndpoints();
app.MapCatalogEndpoints();
app.MapOrderingEndpoints();
app.MapPaymentsEndpoints();
app.MapDeliveryEndpoints();
app.MapNotificationsEndpoints();
app.MapIntegrationsEndpoints();

app.Run();

// Umożliwia testy integracyjne (WebApplicationFactory) w przyszłości.
public partial class Program { }
