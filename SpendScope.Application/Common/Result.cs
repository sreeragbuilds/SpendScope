namespace SpendScope.Application.Common;

/// <summary>Outcome of an operation that returns a value: either the value or an <see cref="Common.Error"/>.</summary>
public readonly record struct Result<T>(T? Value, Error? Error)
{
    public bool IsSuccess => Error is null;

    public static Result<T> Ok(T value) => new(value, null);

    public static Result<T> Fail(Error error) => new(default, error);

#pragma warning disable CA2225 // Named alternates Ok/Fail exist.
    public static implicit operator Result<T>(T value) => Ok(value);

    public static implicit operator Result<T>(Error error) => Fail(error);
#pragma warning restore CA2225
}

/// <summary>Outcome of an operation with no return value.</summary>
public readonly record struct Result(Error? Error)
{
    public bool IsSuccess => Error is null;

    public static Result Success { get; } = new(null);

    public static Result Fail(Error error) => new(error);

#pragma warning disable CA2225 // Named alternate Fail exists.
    public static implicit operator Result(Error error) => Fail(error);
#pragma warning restore CA2225
}
