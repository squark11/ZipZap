using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ZipZap.BuildingBlocks.Outbox;

/// <summary>Nazwy tabeli i kolumn outboxa modułu wzięte z modelu EF (do zapytań SQL z blokadą wierszy).</summary>
internal sealed class OutboxTable
{
    public string Module { get; }
    public string Table { get; }
    private readonly IEntityType _entity;
    private readonly StoreObjectIdentifier _store;

    public OutboxTable(DbContext db)
    {
        _entity = db.Model.FindEntityType(typeof(OutboxMessage))
            ?? throw new InvalidOperationException($"{db.GetType().Name} nie mapuje {nameof(OutboxMessage)}.");
        var name = _entity.GetTableName()!;
        var schema = _entity.GetSchema();
        _store = StoreObjectIdentifier.Table(name, schema);
        Table = schema is null ? Quote(name) : $"{Quote(schema)}.{Quote(name)}";
        Module = schema ?? db.GetType().Name.Replace("DbContext", string.Empty).ToLowerInvariant();
    }

    public string Col(string property) => Quote(_entity.FindProperty(property)!.GetColumnName(_store)!);

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
