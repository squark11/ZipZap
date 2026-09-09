namespace ZipZap.BuildingBlocks.Auditing;

/// <summary>
/// Rejestr akcji (audyt). Cross-cutting: aktor pobierany z bieżącego użytkownika,
/// zapis best-effort (nie przerywa akcji biznesowej). Implementacja w module Audit.
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(
        string action,
        string entityType,
        string? entityId = null,
        Guid? storeId = null,
        object? details = null,
        CancellationToken ct = default);
}
