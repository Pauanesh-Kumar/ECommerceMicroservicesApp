using UserService.Domain.Errors;

namespace UserService.Domain.Common;

public sealed class Result : ResultBase
{
    private Result(bool isSuccess, Error? error) : base(isSuccess, error) { }

    public static Result Success() => new(true, null);
    public static Result Failure(Error error) => new(false, error);

    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure)
        => IsSuccess ? onSuccess() : onFailure(Error!);

    public override string ToString() => IsSuccess ? "Success" : $"Failure({Error})";
}