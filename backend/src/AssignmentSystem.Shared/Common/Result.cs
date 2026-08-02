namespace AssignmentSystem.Shared.Common;

/// <summary>
/// Result pattern for expected domain/application failures. Handlers return
/// <c>Result&lt;T&gt;</c> instead of throwing for rule violations — no exceptions
/// for control flow. The Api layer maps <c>Error.Type</c> → HTTP status.
/// </summary>
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error Error { get; }
    public bool IsFailure => !IsSuccess;

    private Result(bool isSuccess, T? value, Error error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, Error.None);
    public static Result<T> Failure(Error error) => new(false, default, error);

    /// <summary>Maps a successful value, propagating failures unchanged.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> mapper) =>
        IsSuccess ? Result<TOut>.Success(mapper(Value!)) : Result<TOut>.Failure(Error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}

/// <summary>Non-generic result for operations with no return value.</summary>
public sealed class Result
{
    public bool IsSuccess { get; }
    public Error Error { get; }
    public bool IsFailure => !IsSuccess;

    private Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public Result<T> ToResult<T>(T? value = default) =>
        IsSuccess ? Result<T>.Success(value!) : Result<T>.Failure(Error);
}
