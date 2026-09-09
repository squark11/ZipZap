using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZipZap.BuildingBlocks.Auditing;
using ZipZap.BuildingBlocks.MultiTenancy;
using ZipZap.Modules.Audit.Domain;

namespace ZipZap.Modules.Audit.Infrastructure;

/// <summary>Zapis audytu do bazy. Best-effort: błąd audytu nie przerywa akcji biznesowej.</summary>
public sealed class EfAuditLogger : IAuditLogger
{
    private readonly AuditDbContext _db;
    private readonly ICurrentUser _user;
    private readonly ILogger<EfAuditLogger> _logger;

    public EfAuditLogger(AuditDbContext db, ICurrentUser user, ILogger<EfAuditLogger> logger)
    {
        _db = db;
        _user = user;
        _logger = logger;
    }

    public async Task LogAsync(string action, string entityType, string? entityId = null,
        Guid? storeId = null, object? details = null, CancellationToken ct = default)
    {
        try
        {
            var json = details is null ? null : JsonSerializer.Serialize(details);
            _db.Entries.Add(new AuditEntry(_user.UserId, action, entityType, entityId, storeId, json));
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Zapis audytu nieudany: {Action} {EntityType}", action, entityType);
        }
    }
}
