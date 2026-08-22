namespace Rah_Negar.Foundation.Errors;

public sealed class Result<T>
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, ApplicationError? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public ApplicationError? Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");

    public static Result<T> Success(T value) =>
        new(true, value, null);

    public static Result<T> Failure(ApplicationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(false, default, error);
    }
}
