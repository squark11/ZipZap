namespace ZipZap.Modules.Ordering.Domain;

/// <summary>Naruszenie reguły domenowej modułu Ordering.</summary>
public sealed class OrderingDomainException : Exception
{
    public OrderingDomainException(string message) : base(message) { }
}
