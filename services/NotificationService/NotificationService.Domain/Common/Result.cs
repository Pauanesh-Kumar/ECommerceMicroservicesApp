namespace NotificationService.Domain.Common;

/// <summary>
/// Lightweight success/failure value object.
/// Avoids throwing exceptions for expected failures (e.g. delivery errors).
/// </summary>
public sealed class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }

    private Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);

    public override string ToString() => IsSuccess ? "Success" : $"Failure: {Error}";
}