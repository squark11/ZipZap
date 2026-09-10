using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ZipZap.Api.Middleware;
using ZipZap.Api.Security;
using ZipZap.BuildingBlocks.DependencyInjection;
using ZipZap.BuildingBlocks.Inbox;
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
using ZipZap.Modules.Audit;
using ZipZap.Modules.Audit.Api;

var builder = WebApplication.CreateBuilder(args);

// --- Twardy guard sekretów na produkcji (fail-fast) ---
// Na produkcji NIE pozwalamy wystartować z domyślnymi/deweloperskimi sekretami.
if (builder.Environment.IsProduction())
{
    var cfg = builder.Configuration;
    var problems = new List<string>();

    var key = cfg["Jwt:SigningKey"] ?? "";
    if (key.StartsWith("CHANGE_ME", StringComparison.Ordinal) || key.Length < 32)
        problems.Add("Jwt:SigningKey — ustaw losowy sekret min. 32 znaki (env: Jwt__SigningKey).");

    var conn = cfg.GetConnectionString("Postgres") ?? "";
    if (conn.Length == 0 || conn.Contains("Password=zipzap", StringComparison.OrdinalIgnoreCase))
        problems.Add("ConnectionStrings:Postgres — ustaw produkcyjne hasło bazy (env: ConnectionStrings__Postgres).");

    if ((cfg["RabbitMq:Password"] ?? "") is "" or "zipzap")
        problems.Add("RabbitMq:Password — ustaw produkcyjne hasło (env: RabbitMq__Password).");

    if ((cfg["Payments:Provider"] ?? "mock").Equals("mock", StringComparison.OrdinalIgnoreCase))
        problems.Add("Payments:Provider = 'mock' — na produkcji użyj realnego dostawcy (env: Payments__Provider).");
    if ((cfg["Payments:Mock:Secret"] ?? "") == "mock-dev-secret")
        problems.Add("Payments:Mock:Secret — zmień domyślny sekret (env: Payments__Mock__Secret).");

    if ((cfg["Seed:AdminPassword"] ?? "") is "" or "Admin123!")
        problems.Add("Seed:AdminPassword — ustaw silne hasło administratora startowego (env: Seed__AdminPassword).");

    if (problems.Count > 0)
        throw new InvalidOperationException(
            "Konfiguracja produkcyjna niebezpieczna/niekompletna:\n - " +
            string.Join("\n - ", problems) +
            "\nUstaw powyższe zmienne środowiskowe (patrz PRODUCTION_SETUP.md).");
}

// --- Wspólny rdzeń (event bus in-process/RabbitMQ, outbox dispatcher, kontekst najemcy) ---
builder.Services.AddBuildingBlocks(builder.Configuration);

// --- Moduły ---
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddOrderingModule(builder.Configuration);
builder.Services.AddPaymentsModule(builder.Configuration);
builder.Services.AddDeliveryModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddIntegrationsModule();
builder.Services.AddAuditModule(builder.Configuration);

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

// Jednolita koperta błędu (RFC7807 + code + traceId) dla wszystkich odpowiedzi
// błędnych, w tym nieobsłużonych wyjątków (żadnych stack trace do klienta).
builder.Services.AddExceptionHandler<ZipZap.Api.BadRequestExceptionHandler>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        var traceId = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
        ctx.ProblemDetails.Extensions["traceId"] = traceId;
        ctx.ProblemDetails.Extensions["code"] =
            ctx.ProblemDetails.Title ?? (ctx.ProblemDetails.Status == 500 ? "internal_error" : "error");
    };
});

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

// Globalny handler wyjątków → spójna koperta ProblemDetails (bez stack trace do klienta).
app.UseExceptionHandler();
app.UseStatusCodePages();

// Correlation id + logowanie żądań: id z nagłówka X-Correlation-Id lub generowany;
// wrzucany do zakresu logów (każda linia w żądaniu ma cid) i odsyłany w odpowiedzi.
var httpLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("HttpRequest");
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers.TryGetValue("X-Correlation-Id", out var incoming)
                        && !string.IsNullOrWhiteSpace(incoming)
        ? incoming.ToString()
        : Activity.Current?.Id ?? context.TraceIdentifier;
    context.Response.Headers["X-Correlation-Id"] = correlationId;

    using (httpLogger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await next();
        }
        finally
        {
            sw.Stop();
            var path = context.Request.Path.Value ?? string.Empty;
            // Health-checki są częste — logujemy je ciszej (Debug).
            var level = path.StartsWith("/health") ? LogLevel.Debug : LogLevel.Information;
            httpLogger.Log(level, "{Method} {Path} -> {StatusCode} ({ElapsedMs} ms) [cid={CorrelationId}]",
                context.Request.Method, path, context.Response.StatusCode, sw.ElapsedMilliseconds, correlationId);
        }
    }
});

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

// Liveness — proces żyje.
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "ZipZap.Api", timeUtc = DateTime.UtcNow }))
    .WithName("Health").WithTags("System");
app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).WithTags("System");

// Readiness — zależności (baza) osiągalne.
app.MapGet("/health/ready", async (MessagingDbContext db, CancellationToken ct) =>
{
    var dbOk = await db.Database.CanConnectAsync(ct);
    return dbOk
        ? Results.Ok(new { status = "ready", database = "up" })
        : Results.Json(new { status = "not-ready", database = "down" }, statusCode: StatusCodes.Status503ServiceUnavailable);
}).WithTags("System");

// --- Endpointy modułów ---
app.MapIdentityEndpoints();
app.MapCatalogEndpoints();
app.MapOrderingEndpoints();
app.MapPaymentsEndpoints();
app.MapDeliveryEndpoints();
app.MapNotificationsEndpoints();
app.MapIntegrationsEndpoints();
app.MapAuditEndpoints();

app.Run();

// Udostępnia klasę Program dla testów integracyjnych (WebApplicationFactory<Program>).
public partial class Program { }

// Umożliwia testy integracyjne (WebApplicationFactory) w przyszłości.
public partial class Program { }
