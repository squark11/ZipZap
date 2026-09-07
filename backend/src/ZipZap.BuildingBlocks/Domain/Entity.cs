namespace ZipZap.BuildingBlocks.Domain;

/// <summary>
/// Bazowa encja domenowa. Tożsamość oparta o <see cref="Id"/>.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public override bool Equals(object? obj)
        => obj is Entity other && other.GetType() == GetType() && other.Id == Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
