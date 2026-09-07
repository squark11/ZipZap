namespace ZipZap.BuildingBlocks.Domain;

/// <summary>Ustandaryzowany błąd domenowy/aplikacyjny.</summary>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error NotFound(string message) => new("not_found", message);
    public static Error Validation(string message) => new("validation", message);
    public static Error Conflict(string message) => new("conflict", message);
    public static Error Unauthorized(string message) => new("unauthorized", message);
    public static Error Forbidden(string message) => new("forbidden", message);
}

/// <summary>Wynik operacji bez wartości.</summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        switch (isSuccess)
        {
            case true when error != Error.None:
                throw new InvalidOperationException("Sukces nie może mieć błędu.");
            case false when error == Error.None:
                throw new InvalidOperationException("Porażka musi mieć błąd.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => new(value, true, Error.None);
    public static Result<T> Failure<T>(Error error) => new(default, false, error);
}

/// <summary>Wynik operacji z wartością.</summary>
public class Result<T> : Result
{
    private readonly T? _value;

    protected internal Result(T? value, bool isSuccess, Error error) : base(isSuccess, error)
        => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Brak wartości dla nieudanego wyniku.");

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure<T>(error);
}
