using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ZipZap.Api.Configuration;
using ZipZap.Api.Middleware;
using ZipZap.Api.Security;
using ZipZap.BuildingBlocks.DependencyInjection;
using ZipZap.BuildingBlocks.Domain;
using ZipZap.BuildingBlocks.Inbox;
using ZipZap.BuildingBlocks.MultiTenancy;
using ZipZap.BuildingBlocks.Persistence;
using ZipZap.BuildingBlocks.Security;
using ZipZap.Modules.Catalog.Application;
using ZipZap.Modules.Identity.Application;
using ZipZap.Modules.Ordering.Application;
using ZipZap.Modules.Identity;
using ZipZap.Modules.Identity.Api;
using ZipZap.Modules.Catalog;
using ZipZap.Modules.Catalog.Api;
using ZipZap.Modules.Ordering;
using ZipZap.Modules.Ordering.Api;
using ZipZap.Modules.Payments;
using ZipZap.Modules.Payments.Api;
using ZipZap.Modules.Payments.Infrastructure;
using ZipZap.Modules.Delivery;
using ZipZap.Modules.Delivery.Api;
using ZipZap.Modules.Notifications;
using ZipZap.Modules.Notifications.Api;
using ZipZap.Modules.Integrations;
using ZipZap.Modules.Integrations.Api;
using ZipZap.Modules.Audit;
using ZipZap.Modules.Audit.Api;
using ZipZap.Modules.Feedback;
using ZipZap.Modules.Feedback.Api;

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
builder.Services.AddFeedbackModule(builder.Configuration);

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

builder.Services.AddSingleton<PlatformSettingsStore>();

// Data Protection — szyfrowanie sekretów integracji at-rest; klucze utrwalane
// (inaczej po restarcie nie odszyfrujemy zapisanych tokenów).
builder.Services.AddDataProtection()
    .SetApplicationName("ZipZap")
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));
builder.Services.AddSingleton<StoreIntegrationStore>();
// Nadpisz domyślny (null) resolver realnym adapterem nad magazynem integracji sklepów.
builder.Services.AddSingleton<ZipZap.Modules.Payments.Application.IStorePaymentGateway, StorePaymentGatewayAdapter>();
builder.Services.AddSingleton<StoreBillingStore>();
builder.Services.AddSingleton<StoreLegalStore>();
// Nadpisz domyślny (null) provider polityki prawnej sklepu adapterem nad magazynem dokumentów.
builder.Services.AddSingleton<ZipZap.Modules.Ordering.Application.IStoreLegalPolicyProvider, StoreLegalPolicyAdapter>();
builder.Services.AddSingleton<PlatformIntegrationsStore>();
// Realny sender e-mail. Priorytet: HTTP API dostawcy (Resend/Brevo, port 443 — Render blokuje SMTP),
// z fallbackiem na SMTP/MailKit (lokalnie / hosting bez blokady portów). Nadpisuje mock LoggingEmailSender.
builder.Services.AddScoped<SmtpEmailSender>();
builder.Services.AddHttpClient<ZipZap.Modules.Identity.Application.IEmailSender, HttpEmailSender>();
// Integracje platformy edytowalne w panelu — nadpisz domyślne (env) źródło Google Client ID.
builder.Services.AddSingleton<ZipZap.Modules.Identity.Application.IGoogleClientIdProvider, PlatformGoogleClientIdProvider>();
// Captcha (Cloudflare Turnstile) — weryfikacja po stronie serwera; wyłączona, dopóki niekonfigurowana w panelu.
builder.Services.AddHttpClient<ZipZap.BuildingBlocks.Security.ICaptchaVerifier, TurnstileCaptchaVerifier>();

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

// Za odwrotnym proxy (Fly/hosting kończą TLS) — honoruj X-Forwarded-Proto/For,
// aby Request.Scheme = https. Bez tego absolutne URL-e (logo sklepu, obrazki, linki,
// redirecty płatności) generowałyby się jako http → mixed content na stronie https.
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor,
};
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

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

// Serwuje statyczne zasoby (m.in. /mock/* — grafiki demo dla seeda pilotażu).
app.UseStaticFiles();

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

// Status konfiguracji (tylko admin) — SAME FLAGI, bez żadnych sekretów.
app.MapGet("/api/admin/config/status",
    async (IConfiguration cfg, IHostEnvironment env,
        ZipZap.Modules.Identity.Application.IGoogleClientIdProvider google, CancellationToken ct) =>
{
    bool set(string key) => !string.IsNullOrWhiteSpace(cfg[key]);
    var jwt = cfg["Jwt:SigningKey"] ?? string.Empty;
    var googleId = await google.GetClientIdAsync(ct); // panel → env (efektywna wartość)
    return Results.Ok(new
    {
        environment = env.EnvironmentName,
        payments = new
        {
            provider = cfg["Payments:Provider"],
            publicUrl = cfg["Payments:PublicUrl"],
            mockPayPage = set("Payments:Mock:PayPageUrl"),
        },
        googleSignIn = !string.IsNullOrWhiteSpace(googleId),
        email = set("Email:Http:ApiKey") || set("Email:Smtp:Host"),
        emailChannel = set("Email:Http:ApiKey")
            ? "http:" + (cfg["Email:Http:Provider"] ?? "resend")
            : (set("Email:Smtp:Host") ? "smtp" : "none"),
        rabbitMq = set("RabbitMq:Host"),
        identityPublicUrl = cfg["Identity:PublicUrl"],
        adminSeedEmail = cfg["Seed:AdminEmail"],
        jwtUsingDevSecret = jwt.Contains("CHANGE_ME") || jwt.Contains("DEV_ONLY"),
    });
}).RequireAuthorization("Admin").WithTags("System");

// Integracje platformy edytowalne w panelu (Admin) — zamiast env. Sekret captchy write-only.
app.MapGet("/api/admin/config/integrations", async (PlatformIntegrationsStore store, CancellationToken ct) =>
    Results.Ok(await store.GetStatusAsync(ct))).RequireAuthorization("Admin").WithTags("System");

app.MapPut("/api/admin/config/integrations",
    async (PlatformIntegrationsUpdate body, PlatformIntegrationsStore store, CancellationToken ct) =>
{
    var err = PlatformIntegrationsStore.Validate(body);
    if (err is not null) return Results.Problem(detail: err, statusCode: 400, title: "validation");
    // Włączenie captchy wymaga sekretu (podanego teraz lub już zapisanego).
    if (!string.IsNullOrWhiteSpace(body.CaptchaProvider) && string.IsNullOrWhiteSpace(body.CaptchaSecret)
        && !(await store.GetStatusAsync(ct)).HasCaptchaSecret)
        return Results.Problem(detail: "Włączenie captchy wymaga sekretu (secret key).", statusCode: 400, title: "validation");
    return Results.Ok(await store.SaveAsync(body, ct));
}).RequireAuthorization("Admin").WithTags("System");

// Test wysyłki e-mail — z panelu admina. Działa dla aktywnego kanału (HTTP API lub SMTP).
// `to` domyślnie = adres nadawcy.
app.MapPost("/api/admin/config/smtp/test",
    async (string? to, ZipZap.Modules.Identity.Application.IEmailSender email,
        PlatformIntegrationsStore store, IConfiguration cfg, CancellationToken ct) =>
{
    var httpEnabled = !string.IsNullOrWhiteSpace(cfg["Email:Http:ApiKey"]);
    var smtp = await store.GetSmtpAsync(ct);
    if (!httpEnabled && !smtp.Enabled)
        return Results.Problem(detail: "Poczta nie jest skonfigurowana (ani HTTP API, ani SMTP).", statusCode: 400, title: "validation");

    var recipient = !string.IsNullOrWhiteSpace(to) ? to!.Trim()
        : (smtp.FromEmail ?? cfg["Email:Http:FromEmail"] ?? cfg["Email:Smtp:FromEmail"]);
    if (string.IsNullOrWhiteSpace(recipient))
        return Results.Problem(detail: "Podaj adres odbiorcy testu.", statusCode: 400, title: "validation");

    var channel = httpEnabled ? "http:" + (cfg["Email:Http:Provider"] ?? "resend") : "smtp";
    try
    {
        await email.SendAsync(new ZipZap.Modules.Identity.Application.EmailMessage(
            recipient, "Dowózka.pl — test poczty",
            "To testowa wiadomość z panelu Dowózka.pl. Jeśli ją widzisz — konfiguracja poczty działa."), ct);
        return Results.Ok(new { sent = true, to = recipient, channel });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: "Wysyłka nie powiodła się: " + ex.Message, statusCode: 400, title: "email_error");
    }
}).RequireAuthorization("Admin").WithTags("System");

// Publiczna konfiguracja dla aplikacji klienta — Google Client ID + captcha (provider + site key, jawne).
app.MapGet("/api/config/public",
    async (IGoogleClientIdProvider google, PlatformIntegrationsStore store, CancellationToken ct) =>
{
    var googleId = await google.GetClientIdAsync(ct);
    var status = await store.GetStatusAsync(ct);
    var captchaEnabled = !string.IsNullOrWhiteSpace(status.CaptchaProvider)
        && !string.IsNullOrWhiteSpace(status.CaptchaSiteKey) && status.HasCaptchaSecret;
    return Results.Ok(new
    {
        googleClientId = googleId,
        googleSignInEnabled = !string.IsNullOrWhiteSpace(googleId),
        captchaProvider = captchaEnabled ? status.CaptchaProvider : null,
        captchaSiteKey = captchaEnabled ? status.CaptchaSiteKey : null,
    });
}).WithTags("System");

// Multi-lokalizacja: właściciel sklepu (StoreEmployee) lub Admin dodaje kolejną LOKALIZACJĘ
// (nowy sklep) przypisaną DO SIEBIE. Po sukcesie aplikacja/panel odświeża token (nowy store_id).
app.MapPost("/api/merchant/stores",
    async (CreateStoreRequest req, ICurrentUser user, CatalogService catalog, IdentityService identity, CancellationToken ct) =>
{
    IResult Problem(Error e) => Results.Problem(detail: e.Message, statusCode: e.ToStatusCode(), title: e.Code);

    if (user.UserId is not Guid uid)
        return Results.Problem(detail: "Brak tożsamości.", statusCode: 401, title: "unauthorized");
    if (!(user.Roles.Contains("Admin") || user.StoreIds.Count > 0))
        return Results.Problem(detail: "Tylko właściciel sklepu może dodać lokalizację.", statusCode: 403, title: "forbidden");

    var created = await catalog.CreateStoreAsync(req.Name, req.Slug, req.Description, req.City, req.Address, req.Phone,
        req.CommissionRate, req.MinimumOrderValue, ct, req.LogoUrl, req.Latitude, req.Longitude);
    if (created.IsFailure) return Problem(created.Error);

    var assign = await identity.AssignStoreEmployeeAsync(uid, created.Value.Id, ct);
    if (assign.IsFailure) return Problem(assign.Error);

    return Results.Ok(created.Value);
}).RequireAuthorization().WithTags("Catalog");

// --- Rejestracja per-kanał (P-Role2) ---
// Sklepy i dostawcy rejestrują się przez WEB (panel). Klienci — tylko z aplikacji mobilnej.

// Publiczny „Załóż sklep": tworzy sklep + konto właściciela (StoreEmployee) i loguje.
app.MapPost("/api/register/store",
    async (RegisterStoreRequest req, ICaptchaVerifier captcha, CatalogService catalog, IdentityService identity, CancellationToken ct) =>
{
    IResult Problem(Error e) => Results.Problem(detail: e.Message, statusCode: e.ToStatusCode(), title: e.Code);

    if (!await captcha.VerifyAsync(req.CaptchaToken, ct))
        return Results.Problem(detail: "Weryfikacja captcha nie powiodła się.", statusCode: 400, title: "captcha");
    if (!NipValidator.IsValid(req.Nip))
        return Results.Problem(detail: "Podaj poprawny NIP (10 cyfr).", statusCode: 400, title: "validation");
    // Sprawdź e-mail PRZED utworzeniem sklepu, by nie zostawić osieroconego sklepu.
    if (!await identity.IsEmailAvailableAsync(req.Email, ct))
        return Results.Problem(detail: "Użytkownik z tym adresem e-mail już istnieje.", statusCode: 409, title: "conflict");

    var store = await catalog.CreateStoreAsync(req.StoreName, null, null, req.City, null, req.Phone,
        0.10m, 0m, ct, null, null, null, req.Nip);
    if (store.IsFailure) return Problem(store.Error);

    var auth = await identity.RegisterStoreOwnerAsync(req.Email, req.Password, req.FullName, req.Phone, store.Value.Id, ct);
    if (auth.IsFailure) return Problem(auth.Error);
    return Results.Ok(auth.Value);
}).WithTags("Registration");

// Publiczny „Zostań dostawcą": konto Driver, nieaktywne (do weryfikacji przez administratora).
app.MapPost("/api/register/driver",
    async (RegisterDriverRequest req, ICaptchaVerifier captcha, IdentityService identity, CancellationToken ct) =>
{
    if (!await captcha.VerifyAsync(req.CaptchaToken, ct))
        return Results.Problem(detail: "Weryfikacja captcha nie powiodła się.", statusCode: 400, title: "captcha");

    var res = await identity.RegisterDriverAsync(req.Email, req.Password, req.FullName, req.Phone, ct);
    if (res.IsFailure) return Results.Problem(detail: res.Error.Message, statusCode: res.Error.ToStatusCode(), title: res.Error.Code);
    return Results.Ok(new { status = "pending", message = "Dziękujemy! Zgłoszenie czeka na weryfikację — damy znać po aktywacji konta." });
}).WithTags("Registration");

// Administrator serwisu: oczekujący dostawcy + zatwierdzenie (aktywacja + przypisanie do sklepu).
app.MapGet("/api/admin/drivers/pending", async (IdentityService identity, CancellationToken ct) =>
    Results.Ok(await identity.ListPendingDriversAsync(ct))).RequireAuthorization("Admin").WithTags("Registration");

app.MapPost("/api/admin/drivers/{userId:guid}/approve",
    async (Guid userId, ApproveDriverRequest req, IdentityService identity, CancellationToken ct) =>
{
    var res = await identity.ApproveDriverAsync(userId, req.StoreId, ct);
    return res.IsFailure
        ? Results.Problem(detail: res.Error.Message, statusCode: res.Error.ToStatusCode(), title: res.Error.Code)
        : Results.Ok();
}).RequireAuthorization("Admin").WithTags("Registration");

// Administrator serwisu: tabela użytkowników + aktywacja/dezaktywacja konta.
app.MapGet("/api/admin/users", async (IdentityService identity, CancellationToken ct) =>
    Results.Ok(await identity.ListUsersAsync(ct))).RequireAuthorization("Admin").WithTags("Admin");

app.MapPost("/api/admin/users/{userId:guid}/active",
    async (Guid userId, bool value, IdentityService identity, CancellationToken ct) =>
{
    var res = await identity.SetUserActiveAsync(userId, value, ct);
    return res.IsFailure
        ? Results.Problem(detail: res.Error.Message, statusCode: res.Error.ToStatusCode(), title: res.Error.Code)
        : Results.Ok();
}).RequireAuthorization("Admin").WithTags("Admin");

// Administrator serwisu: statystyki platformy (przegląd na pulpicie admina).
app.MapGet("/api/admin/stats",
    async (IdentityService identity, CatalogService catalog, OrderingService ordering, CancellationToken ct) =>
{
    var stores = await catalog.ListStoresAsync(false, null, null, ct);
    var users = await identity.UserCountsAsync(ct);
    var pending = await identity.ListPendingDriversAsync(ct);
    var (orderCount, gmv) = await ordering.PlatformOrderStatsAsync(ct);
    return Results.Ok(new
    {
        stores = new { total = stores.Count, active = stores.Count(s => s.IsActive) },
        users = new { total = users.Total, customers = users.Customers, stores = users.Stores, drivers = users.Drivers, inactive = users.Inactive },
        pendingDrivers = pending.Count,
        orders = new { count = orderCount, gmv },
    });
}).RequireAuthorization("Admin").WithTags("Admin");

// Sklepy dowożące pod kod pocztowy klienta. Bez kodu → wszystkie (posortowane wg odległości gdy lat/lng).
// Zasięg = aktywna strefa sklepu, która obejmuje kod (pusta lista kodów strefy = obsługuje wszędzie).
app.MapGet("/api/stores/serving",
    async (string? postalCode, double? lat, double? lng, CatalogService catalog, OrderingService ordering, CancellationToken ct) =>
{
    var stores = await catalog.ListStoresAsync(true, lat, lng, ct);
    var digits = new string((postalCode ?? "").Where(char.IsDigit).ToArray());
    if (digits.Length != 5) return Results.Ok(stores);
    var serving = await ordering.StoresServingPostalCodeAsync(postalCode!, ct);
    return Results.Ok(stores.Where(s => serving.Contains(s.Id)).ToList());
}).WithTags("Catalog");

// Onboarding: status gotowości sklepu do sprzedaży (checklista). Agreguje Catalog + Ordering + dokumenty/integracje.
app.MapGet("/api/stores/{storeId:guid}/readiness",
    async (Guid storeId, ICurrentUser user, CatalogService catalog, OrderingService ordering,
           StoreLegalStore legalStore, StoreIntegrationStore integrationStore, CancellationToken ct) =>
{
    if (!user.ManagesStore(storeId))
        return Results.Problem(detail: "Brak dostępu do tego sklepu.", statusCode: 403, title: "forbidden");

    var storeRes = await catalog.GetStoreAsync(storeId.ToString(), ct);
    if (storeRes.IsFailure)
        return Results.Problem(detail: storeRes.Error.Message, statusCode: storeRes.Error.ToStatusCode(), title: storeRes.Error.Code);
    var store = storeRes.Value;

    var hasProduct = (await catalog.ListProductsAsync(storeId, null, ct)).Any(p => p.IsAvailable);
    var hasZone = (await ordering.ListZonesAsync(storeId, ct)).Any(z => z.IsActive);
    var hasSlot = (await ordering.ListAvailableSlotsAsync(storeId, null, null, ct)).Count > 0;
    var legal = await legalStore.GetAsync(storeId, ct);
    var legalOk = !legal.RequiresAcceptance
        || (!string.IsNullOrWhiteSpace(legal.TermsUrl) && !string.IsNullOrWhiteSpace(legal.PrivacyUrl));
    var paymentOk = !string.IsNullOrWhiteSpace((await integrationStore.GetStatusAsync(storeId, ct)).Provider);
    var published = store.IsActive && store.Status == "Open";

    var steps = new[]
    {
        new { key = "logo",     label = "Logo sklepu",        done = !string.IsNullOrWhiteSpace(store.LogoUrl),          required = false, hint = "Dodaj logo w sekcji 'Profil sklepu' poniżej." },
        new { key = "location", label = "Lokalizacja (mapa)", done = store.Latitude.HasValue && store.Longitude.HasValue, required = false, hint = "Podaj współrzędne, by klienci widzieli sklep wg odległości." },
        new { key = "products", label = "Oferta (produkty)",  done = hasProduct,                                          required = true,  hint = "Dodaj co najmniej jeden dostępny produkt (zakładka Oferta)." },
        new { key = "legal",    label = "Dokumenty prawne",   done = legalOk,                                             required = true,  hint = "Podaj regulamin i politykę prywatności (zakładka Integracje)." },
        new { key = "payment",  label = "Bramka płatnicza",   done = paymentOk,                                           required = false, hint = "Podłącz swoją bramkę (zakładka Integracje). Na pilotaż działa tryb mock." },
        new { key = "zone",     label = "Strefa dostawy",     done = hasZone,                                             required = true,  hint = "Dodaj strefę dostawy w sekcji 'Dostawa' poniżej." },
        new { key = "slot",     label = "Termin dostawy",     done = hasSlot,                                             required = true,  hint = "Dodaj przyszły termin dostawy w sekcji 'Dostawa' poniżej." },
        new { key = "published",label = "Sklep opublikowany", done = published,                                           required = true,  hint = "Ustaw status 'Otwarty', gdy wszystko gotowe." },
    };
    return Results.Ok(new { storeId, readyToSell = steps.Where(s => s.required).All(s => s.done), steps });
}).RequireAuthorization("StoreEmployee").WithTags("Catalog");

// Seed pilotażu (Admin): tworzy sklepy demo (Rapacz/Lewiatan) z logo, produktami (zdjęcia),
// strefą i terminem dostawy — gotowe do sprzedaży i widoczne wg odległości. Idempotentne (po slug).
app.MapPost("/api/admin/seed/pilot",
    async (HttpContext http, CatalogService catalog, OrderingService ordering, CancellationToken ct) =>
{
    var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
    var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));
    var demoDesc = "Sklep demonstracyjny — dane przykładowe do podmiany przez sklep.";

    var stores = new[]
    {
        new { slug = "demo-rapacz-rynek", name = "Rapacz — Rynek", city = "Kraków", address = "Rynek Główny 5",
              phone = "12 555 10 10", lat = 50.0616, lng = 19.9366, logo = "stores/rapacz.png", min = 20m },
        new { slug = "demo-lewiatan-podwawelskie", name = "Lewiatan — Podwawelskie", city = "Kraków", address = "ul. Komandosów 12",
              phone = "12 555 20 20", lat = 50.0455, lng = 19.9295, logo = "stores/lewiatan.png", min = 15m },
    };
    var products = new[]
    {
        new { name = "Jabłka", price = 4.99m, unit = "kg", img = "food/jablka.jpg",
              options = (ZipZap.Contracts.Catalog.ProductUnitOption[]?)new[] {
                  new ZipZap.Contracts.Catalog.ProductUnitOption("kg", 4.99m),
                  new ZipZap.Contracts.Catalog.ProductUnitOption("szt", 1.20m) } },
        new { name = "Mleko 2%", price = 3.49m, unit = "szt", img = "food/mleko.jpg", options = (ZipZap.Contracts.Catalog.ProductUnitOption[]?)null },
        new { name = "Parmezan", price = 12.90m, unit = "100g", img = "food/parmezan.jpg", options = (ZipZap.Contracts.Catalog.ProductUnitOption[]?)null },
        new { name = "Szynka", price = 8.99m, unit = "100g", img = "food/szynka.jpg", options = (ZipZap.Contracts.Catalog.ProductUnitOption[]?)null },
    };

    var created = new List<string>();
    var refreshed = new List<string>();

    foreach (var s in stores)
    {
        // Istnieje już — odśwież tylko URL-e logo/zdjęć do bieżącego (https za proxy) baseUrl.
        var existing = await catalog.GetStoreAsync(s.slug, ct);
        if (existing.IsSuccess)
        {
            var sid = existing.Value.Id;
            await catalog.UpdateStoreAsync(sid, null, null, null, null, ct, logoUrl: $"{baseUrl}/mock/{s.logo}");
            var existingProducts = await catalog.ListProductsAsync(sid, null, ct);
            foreach (var p in products)
            {
                var match = existingProducts.FirstOrDefault(x => x.Name == p.name);
                if (match is not null)
                    await catalog.UpdateProductAsync(match.Id, null, null, null, null, null, null, $"{baseUrl}/mock/{p.img}", p.options, ct);
            }
            refreshed.Add(s.slug);
            continue;
        }

        var storeRes = await catalog.CreateStoreAsync(s.name, s.slug, demoDesc, s.city, s.address, s.phone,
            0.10m, s.min, ct, $"{baseUrl}/mock/{s.logo}", s.lat, s.lng);
        if (storeRes.IsFailure) return Results.Problem(detail: storeRes.Error.Message, statusCode: storeRes.Error.ToStatusCode(), title: storeRes.Error.Code);
        var storeId = storeRes.Value.Id;

        var cat = await catalog.CreateCategoryAsync(storeId, "Spożywcze", 0, null, ct);
        var catId = cat.IsSuccess ? cat.Value.Id : (Guid?)null;
        foreach (var p in products)
            await catalog.CreateProductAsync(storeId, catId, p.name, null, p.price, "PLN", p.unit, null, $"{baseUrl}/mock/{p.img}", p.options, ct);

        var zone = await ordering.CreateZoneAsync(storeId, "Centrum", 8.00m, null, ct);
        if (zone.IsSuccess)
            await ordering.CreateSlotAsync(storeId, zone.Value.Id, tomorrow, new TimeOnly(10, 0), new TimeOnly(12, 0), 20, ct);

        created.Add(s.slug);
    }

    return Results.Ok(new { created, refreshed, note = "Sklepy demo gotowe (URL-e logo/zdjęć odświeżone). Zaloguj się jako sklep, by je edytować lub usunąć." });
}).RequireAuthorization("Admin").WithTags("System");

// Ustawienia platformy (edytowalne, nie‑sekretne) — odczyt i zapis (admin).
app.MapGet("/api/admin/config/platform", async (PlatformSettingsStore store, CancellationToken ct) =>
    Results.Ok(await store.GetAsync(ct))).RequireAuthorization("Admin").WithTags("System");

app.MapPut("/api/admin/config/platform", async (PlatformSettings body, PlatformSettingsStore store, CancellationToken ct) =>
    Results.Ok(await store.SaveAsync(body, ct))).RequireAuthorization("Admin").WithTags("System");

// Integracja płatności per-sklep (każdy sklep podpina swoje konto). Sekrety write-only,
// szyfrowane; zwracamy TYLKO status. Dostęp: admin lub pracownik TEGO sklepu.
app.MapGet("/api/payments/stores/{storeId:guid}/integration",
    async (Guid storeId, ICurrentUser user, StoreIntegrationStore store, CancellationToken ct) =>
{
    var ok = user.ManagesStore(storeId);
    if (!ok) return Results.Problem(detail: "Brak dostępu do integracji tego sklepu.", statusCode: 403, title: "forbidden");
    return Results.Ok(await store.GetStatusAsync(storeId, ct));
}).RequireAuthorization("StoreEmployee").WithTags("Payments");

app.MapPut("/api/payments/stores/{storeId:guid}/integration",
    async (Guid storeId, StoreIntegrationUpdate body, ICurrentUser user, StoreIntegrationStore store, CancellationToken ct) =>
{
    var ok = user.ManagesStore(storeId);
    if (!ok) return Results.Problem(detail: "Brak dostępu do integracji tego sklepu.", statusCode: 403, title: "forbidden");
    return Results.Ok(await store.SaveAsync(storeId, body, ct));
}).RequireAuthorization("StoreEmployee").WithTags("Payments");

// Dokumenty prawne sklepu (regulamin / polityka prywatności / RODO) + wymóg akceptacji.
// Odczyt PUBLICZNY — checkout pokazuje linki i wymóg akceptacji jeszcze przed zalogowaniem.
app.MapGet("/api/stores/{storeId:guid}/legal", async (Guid storeId, StoreLegalStore store, CancellationToken ct) =>
    Results.Ok(await store.GetAsync(storeId, ct))).WithTags("Legal");

app.MapPut("/api/stores/{storeId:guid}/legal",
    async (Guid storeId, StoreLegal body, ICurrentUser user, StoreLegalStore store, CancellationToken ct) =>
{
    var ok = user.ManagesStore(storeId);
    if (!ok) return Results.Problem(detail: "Brak dostępu do dokumentów tego sklepu.", statusCode: 403, title: "forbidden");
    var err = StoreLegalStore.Validate(body);
    if (err is not null) return Results.Problem(detail: err, statusCode: 400, title: "validation");
    return Results.Ok(await store.SaveAsync(storeId, body, ct));
}).RequireAuthorization("StoreEmployee").WithTags("Legal");

// Plan rozliczeniowy sklepu (A = dostawa ZipZap / B = kurier sklepu) — odczyt i zapis.
app.MapGet("/api/payments/stores/{storeId:guid}/billing",
    async (Guid storeId, ICurrentUser user, StoreBillingStore billing, CancellationToken ct) =>
{
    var ok = user.ManagesStore(storeId);
    if (!ok) return Results.Problem(detail: "Brak dostępu do rozliczeń tego sklepu.", statusCode: 403, title: "forbidden");
    return Results.Ok(await billing.GetAsync(storeId, ct));
}).RequireAuthorization("StoreEmployee").WithTags("Payments");

app.MapPut("/api/payments/stores/{storeId:guid}/billing",
    async (Guid storeId, StoreBilling body, ICurrentUser user, StoreBillingStore billing, CancellationToken ct) =>
{
    var ok = user.ManagesStore(storeId);
    if (!ok) return Results.Problem(detail: "Brak dostępu do rozliczeń tego sklepu.", statusCode: 403, title: "forbidden");
    return Results.Ok(await billing.SaveAsync(storeId, body, ct));
}).RequireAuthorization("StoreEmployee").WithTags("Payments");

// Faktura miesięczna ZipZap → sklep. Plan A: liczba dostaw × stała opłata; Plan B: suma prowizji.
app.MapGet("/api/payments/stores/{storeId:guid}/invoice",
    async (Guid storeId, string? month, ICurrentUser user, PaymentsDbContext db,
           StoreBillingStore billing, PlatformSettingsStore platform, CancellationToken ct) =>
{
    var ok = user.ManagesStore(storeId);
    if (!ok) return Results.Problem(detail: "Brak dostępu do rozliczeń tego sklepu.", statusCode: 403, title: "forbidden");

    var now = DateTime.UtcNow;
    DateTime start;
    if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParse($"{month}-01", out var m))
        start = new DateTime(m.Year, m.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    else
        start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    var end = start.AddMonths(1);

    var entries = await db.CommissionLedger.AsNoTracking()
        .Where(l => l.StoreId == storeId && l.CreatedAtUtc >= start && l.CreatedAtUtc < end)
        .OrderBy(l => l.CreatedAtUtc)
        .Select(l => new { l.OrderId, l.Amount, l.CreatedAtUtc })
        .ToListAsync(ct);

    var plan = (await billing.GetAsync(storeId, ct)).Plan;
    var settings = await platform.GetAsync(ct);
    var deliveryFee = settings.ZipZapDeliveryFee;
    var isPlanA = plan == "A";

    var lines = entries.Select(e => new
    {
        e.OrderId,
        e.CreatedAtUtc,
        amount = isPlanA ? deliveryFee : e.Amount,
    }).ToList();
    var total = isPlanA ? entries.Count * deliveryFee : entries.Sum(e => e.Amount);

    return Results.Ok(new
    {
        storeId,
        period = start.ToString("yyyy-MM"),
        plan,
        basis = isPlanA ? "delivery" : "commission",
        unitFee = isPlanA ? deliveryFee : (decimal?)null,
        orderCount = entries.Count,
        total,
        currency = settings.Currency,
        lines,
    });
}).RequireAuthorization("StoreEmployee").WithTags("Payments");

// Administrator serwisu: faktury wszystkich sklepów za miesiąc (nadzór rozliczeń ZipZap→sklep).
app.MapGet("/api/admin/invoices",
    async (string? month, CatalogService catalog, PaymentsDbContext db,
           StoreBillingStore billing, PlatformSettingsStore platform, CancellationToken ct) =>
{
    var now = DateTime.UtcNow;
    DateTime start = (!string.IsNullOrWhiteSpace(month) && DateTime.TryParse($"{month}-01", out var m))
        ? new DateTime(m.Year, m.Month, 1, 0, 0, 0, DateTimeKind.Utc)
        : new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    var end = start.AddMonths(1);

    var settings = await platform.GetAsync(ct);
    var fee = settings.ZipZapDeliveryFee;
    var stores = await catalog.ListStoresAsync(false, null, null, ct);

    var ledger = await db.CommissionLedger.AsNoTracking()
        .Where(l => l.CreatedAtUtc >= start && l.CreatedAtUtc < end)
        .GroupBy(l => l.StoreId)
        .Select(g => new { StoreId = g.Key, Count = g.Count(), Sum = g.Sum(x => x.Amount) })
        .ToListAsync(ct);
    var byStore = ledger.ToDictionary(x => x.StoreId, x => (x.Count, x.Sum));

    var rows = new List<object>();
    decimal grand = 0;
    foreach (var s in stores)
    {
        var plan = (await billing.GetAsync(s.Id, ct)).Plan;
        var isA = plan == "A";
        byStore.TryGetValue(s.Id, out var agg);
        var total = isA ? agg.Count * fee : agg.Sum;
        grand += total;
        rows.Add(new { storeId = s.Id, storeName = s.Name, city = s.City, isActive = s.IsActive,
            plan, basis = isA ? "delivery" : "commission", orderCount = agg.Count, total });
    }
    return Results.Ok(new { period = start.ToString("yyyy-MM"), currency = settings.Currency,
        unitFee = fee, grandTotal = grand, stores = rows });
}).RequireAuthorization("Admin").WithTags("Admin");

// Kod wsparcia sklepu — właściciel/Admin (sklep podaje go administratorowi przy prośbie o pomoc).
app.MapGet("/api/stores/{storeId:guid}/support-code",
    async (Guid storeId, CatalogService catalog, CancellationToken ct) =>
{
    var res = await catalog.GetOrCreateSupportCodeAsync(storeId, ct);
    return res.IsSuccess
        ? Results.Ok(new { supportCode = res.Value })
        : Results.Problem(detail: res.Error.Message, statusCode: res.Error.ToStatusCode(), title: res.Error.Code);
}).RequireAuthorization("StoreEmployee").WithTags("Stores");

// Administrator serwisu: wgląd w sklep po kodzie wsparcia (bez kodu — brak wglądu w dane sklepu).
app.MapGet("/api/admin/support",
    async (string? code, CatalogService catalog, CancellationToken ct) =>
{
    var res = await catalog.FindBySupportCodeAsync(code ?? string.Empty, ct);
    return res.IsSuccess
        ? Results.Ok(res.Value)
        : Results.Problem(detail: res.Error.Message, statusCode: res.Error.ToStatusCode(), title: res.Error.Code);
}).RequireAuthorization("Admin").WithTags("Admin");

// --- Endpointy modułów ---
app.MapIdentityEndpoints();
app.MapCatalogEndpoints();
app.MapOrderingEndpoints();
app.MapPaymentsEndpoints();
app.MapDeliveryEndpoints();
app.MapNotificationsEndpoints();
app.MapIntegrationsEndpoints();
app.MapAuditEndpoints();
app.MapFeedbackEndpoints();

app.Run();

// Udostępnia klasę Program dla testów integracyjnych (WebApplicationFactory<Program>).
public partial class Program { }

// Umożliwia testy integracyjne (WebApplicationFactory) w przyszłości.
public partial class Program { }
