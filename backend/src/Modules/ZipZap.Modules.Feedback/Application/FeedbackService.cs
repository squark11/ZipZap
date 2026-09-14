using Microsoft.EntityFrameworkCore;
using ZipZap.BuildingBlocks.Domain;
using ZipZap.BuildingBlocks.MultiTenancy;
using ZipZap.Modules.Feedback.Domain;
using ZipZap.Modules.Feedback.Infrastructure;

namespace ZipZap.Modules.Feedback.Application;

public sealed record FeedbackDto(
    Guid Id, string Type, string Message, Guid? UserId, string? ContactEmail, Guid? StoreId,
    string? Screen, string? AppVersion, string? Platform, string Status, DateTime CreatedAtUtc)
{
    public static FeedbackDto From(FeedbackItem f) => new(
        f.Id, f.Type.ToString(), f.Message, f.UserId, f.ContactEmail, f.StoreId,
        f.Screen, f.AppVersion, f.Platform, f.Status.ToString(), f.CreatedAtUtc);
}

/// <summary>Przypadki użycia Feedback: zgłoszenie (publiczne), lista i zmiana statusu (Admin/pracownik).</summary>
public sealed class FeedbackService
{
    private readonly FeedbackDbContext _db;
    private readonly ICurrentUser _user;

    public FeedbackService(FeedbackDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public async Task<Result<Guid>> SubmitAsync(string type, string message, string? contactEmail,
        Guid? storeId, string? screen, string? appVersion, string? platform, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message)) return Error.Validation("Treść uwagi jest wymagana.");
        if (message.Trim().Length > 4000) return Error.Validation("Uwaga jest zbyt długa (maks. 4000 znaków).");

        var parsedType = Enum.TryParse<FeedbackType>(type, ignoreCase: true, out var t) ? t : FeedbackType.Other;
        var item = new FeedbackItem(parsedType, message, _user.UserId, contactEmail, storeId, screen, appVersion, platform);
        _db.Items.Add(item);
        await _db.SaveChangesAsync(ct);
        return item.Id;
    }

    public async Task<IReadOnlyList<FeedbackDto>> ListAsync(Guid? storeId, string? status, int take, CancellationToken ct)
    {
        var q = _db.Items.AsNoTracking().AsQueryable();

        // Admin widzi wszystkie uwagi; pracownik — tylko uwagi swoich sklepów.
        if (!_user.Roles.Contains("Admin"))
        {
            var myStores = _user.StoreIds.ToList();
            q = q.Where(f => f.StoreId != null && myStores.Contains(f.StoreId.Value));
        }
        if (storeId is Guid s) q = q.Where(f => f.StoreId == s);
        if (Enum.TryParse<FeedbackStatus>(status, ignoreCase: true, out var st)) q = q.Where(f => f.Status == st);

        return await q.OrderByDescending(f => f.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .Select(f => FeedbackDto.From(f)).ToListAsync(ct);
    }

    public async Task<Result> SetStatusAsync(Guid id, string status, CancellationToken ct)
    {
        if (!Enum.TryParse<FeedbackStatus>(status, ignoreCase: true, out var st))
            return Result.Failure(Error.Validation("Nieznany status (New/InProgress/Closed)."));

        var item = await _db.Items.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (item is null) return Result.Failure(Error.NotFound("Uwaga nie istnieje."));
        if (!_user.Roles.Contains("Admin") && !(item.StoreId is Guid sid && _user.StoreIds.Contains(sid)))
            return Result.Failure(Error.Forbidden("Brak dostępu do tej uwagi."));

        item.SetStatus(st);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
