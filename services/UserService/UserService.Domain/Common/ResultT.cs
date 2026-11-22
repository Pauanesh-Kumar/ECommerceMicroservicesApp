using UserService.Domain.Errors;

namespace UserService.Domain.Common;

public sealed class Result<T> : ResultBase
{
    public T? Value { get; }

    private Result(T? value, bool isSuccess, Error? error) : base(isSuccess, error) => Value = value;

    public static Result<T> Success(T value) => new(value, true, null);
    public static Result<T> Failure(Error err) => new(default, false, err);

    public Result<TOut> Map<TOut>(Func<T, TOut> mapper)
        => IsSuccess ? Result<TOut>.Success(mapper(Value!)) : Result<TOut>.Failure(Error!);

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder)
        => IsSuccess ? binder(Value!) : Result<TOut>.Failure(Error!);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
        => IsSuccess ? onSuccess(Value!) : onFailure(Error!);

    public override string ToString() => IsSuccess ? $"Success({Value})" : $"Failure({Error})";
}