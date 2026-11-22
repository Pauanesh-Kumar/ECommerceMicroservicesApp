namespace UserService.API.Extensions;

using UserService.Domain.Common;

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, int successStatus = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
            return successStatus switch
            {
                StatusCodes.Status201Created => Results.Created(string.Empty, result.Value),
                StatusCodes.Status204NoContent => Results.NoContent(),
                _ => Results.Ok(result.Value)
            };

        return MapError(result.Error!);
    }

    public static IResult ToHttpResult(this UserService.Domain.Common.Result result)
        => result.IsSuccess ? Results.NoContent() : MapError(result.Error!);

    private static IResult MapError(UserService.Domain.Errors.Error error) => error.Code switch
    {
        "USER_NOT_FOUND" => Results.NotFound(ProblemResponse(error)),
        "USER_EMAIL_NOT_FOUND" => Results.NotFound(ProblemResponse(error)),
        "USER_EMAIL_EXISTS" => Results.Conflict(ProblemResponse(error)),
        "USER_ALREADY_INACTIVE" => Results.Conflict(ProblemResponse(error)),
        "USER_ALREADY_ACTIVE" => Results.Conflict(ProblemResponse(error)),
        "USER_INVALID_CREDENTIALS" => Results.Unauthorized(),
        "USER_INVALID_TOKEN" => Results.Unauthorized(),
        "USER_ACCOUNT_INACTIVE" => Results.Problem(
            detail: error.Message,
            statusCode: StatusCodes.Status403Forbidden,
            title: "Account Inactive"),
        "USER_ACCOUNT_SUSPENDED" => Results.Problem(
            detail: error.Message,
            statusCode: StatusCodes.Status403Forbidden,
            title: "Account Suspended"),
        "USER_WRONG_PASSWORD" => Results.UnprocessableEntity(ProblemResponse(error)),
        "USER_SAME_PASSWORD" => Results.UnprocessableEntity(ProblemResponse(error)),
        "ROLE_NOT_FOUND_OR_INACTIVE" => Results.NotFound(ProblemResponse(error)),  
        _ => Results.BadRequest(ProblemResponse(error))
    };

    private static object ProblemResponse(UserService.Domain.Errors.Error error) => new
    {
        code = error.Code,
        message = error.Message
    };
}