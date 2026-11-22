using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.Features.Auth.Commands.Login;
using UserService.Application.Features.Users.Commands.DeactivateUser;
using UserService.Application.Features.Users.Commands.RegisterUser;
using UserService.Application.Features.Users.Commands.UpdateUser;
using UserService.Application.Features.Users.Commands.ChangePassword;
using UserService.Application.Features.Users.Commands.AssignUserRole;
using UserService.Application.Features.Users.Queries.GetUser;
using UserService.Application.Features.Users.Queries.GetUsers;
using UserService.Application.Features.Auth.Commands.ExternalLogin;
using UserService.API.Extensions;

namespace UserService.API.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/users").WithTags("Users").WithOpenApi();
        var auth = app.MapGroup("/api/auth").WithTags("Auth").WithOpenApi();

        auth.MapPost("/login", Login).AllowAnonymous().WithSummary("Login and receive JWT token");
        auth.MapPost("/external-login", ExternalLogin).AllowAnonymous().WithSummary("External login with provider token");
        auth.MapGet("/auth-callback", AuthCallback).AllowAnonymous().WithSummary("OAuth callback handler from provider");

        users.MapPost("/register", Register).AllowAnonymous().WithSummary("Register a new user account");

        users.MapGet("/{id:guid}", GetById)
             .RequireAuthorization("Admin")
             .WithSummary("Get user by ID (admin only)");

        users.MapGet("/by-email/{email}", GetByEmail)
             .RequireAuthorization("Admin")
             .WithSummary("Get user by email (admin only)");

        users.MapGet("/", GetPaged)
             .RequireAuthorization("Admin")
             .WithSummary("Get paginated list of users (admin only)");

        users.MapPut("/{id:guid}", Update)
             .RequireAuthorization("User")
             .WithSummary("Update user profile (self-service)");

        users.MapDelete("/{id:guid}/deactivate", Deactivate)
             .RequireAuthorization("Admin")
             .WithSummary("Deactivate a user account (admin only)");

        users.MapPost("/me/change-password", ChangePassword)
             .RequireAuthorization("User")
             .WithSummary("Change the authenticated user's own password");

        users.MapPut("/{id:guid}/role", AssignRole)
             .RequireAuthorization("Admin")
             .WithSummary("Assign a role to a user (admin only)");

        return app;
    }

    private static async Task<IResult> Login([FromBody] LoginRequest req, ISender mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new LoginCommand(req.Email, req.Password), ct);
        return result.IsSuccess
            ? Results.Ok(new { result.Value.AccessToken, result.Value.RefreshToken, result.Value.ExpiresAt })
            : Results.Unauthorized();
    }

    private static async Task<IResult> Register([FromBody] RegisterRequest req, ISender mediator, CancellationToken ct)
    {
        var result = await mediator.Send(
            new RegisterUserCommand(req.Email, req.Password, req.FirstName, req.LastName, req.PhoneNumber, req.RoleId), ct);
        return result.IsSuccess
            ? Results.Created($"/api/users/{result.Value.UserId}", new { userId = result.Value.UserId })
            : Results.Conflict(new { error = result.Error });
    }

    private static async Task<IResult> GetById(Guid id, ISender mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetUserByIdQuery(id), ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetByEmail(string email, ISender mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetUserByEmailQuery(email), ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetPaged(
        [FromQuery] int page, [FromQuery] int pageSize, [FromQuery] string? status,
        ISender mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetUsersPagedQuery(page, pageSize, status), ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Update(Guid id, [FromBody] UpdateRequest req, ISender mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateUserCommand(id, req.FirstName, req.LastName, req.PhoneNumber), ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Deactivate(Guid id, [FromQuery] string? reason, ISender mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new DeactivateUserCommand(id, reason), ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ChangePassword(
        ClaimsPrincipal user, [FromBody] ChangePasswordRequest req, ISender mediator, CancellationToken ct)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Results.Unauthorized();

        var result = await mediator.Send(new ChangePasswordCommand(userId, req.CurrentPassword, req.NewPassword), ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> AssignRole(Guid id, [FromBody] AssignRoleRequest req, ISender mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new AssignUserRoleCommand(id, req.RoleId), ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ExternalLogin([FromBody] ExternalLoginRequest req, ISender mediator, CancellationToken ct)
    {
        if (!Enum.TryParse<ExternalTokenType>(req.TokenType, ignoreCase: true, out var tokenType))
            return Results.BadRequest(new { error = $"Invalid tokenType '{req.TokenType}'. Use 'IdToken' or 'AuthorizationCode'." });

        var command = new ExternalLoginCommand(req.Provider, tokenType, req.Token, req.RedirectUri, req.CodeVerifier);
        var result = await mediator.Send(command, ct);

        return result.IsSuccess
            ? Results.Ok(new
            {
                result.Value.AccessToken,
                result.Value.RefreshToken,
                result.Value.ExpiresAt,
                user = new { result.Value.UserId, result.Value.Email, result.Value.FullName }
            })
            : Results.Json(new { error = result.Error?.Message ?? "Authentication failed" },
                statusCode: StatusCodes.Status401Unauthorized);
    }

    private static IResult AuthCallback(
        [FromQuery] string? code, [FromQuery] string? state,
        [FromQuery] string? error, [FromQuery] string? error_description)
    {
        if (!string.IsNullOrEmpty(error))
            return Results.BadRequest(new { error, description = error_description });
        if (string.IsNullOrEmpty(code))
            return Results.BadRequest(new { error = "No authorization code received" });

        return Results.Ok(new { code, state, message = "Authorization code received. Use this code with /api/auth/external-login endpoint." });
    }
}

public sealed record LoginRequest(string Email, string Password);
public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName, string? PhoneNumber, Guid? RoleId);
public sealed record UpdateRequest(string FirstName, string LastName, string? PhoneNumber);
public sealed record ExternalLoginRequest(string Provider, string TokenType, string Token, string? RedirectUri, string? CodeVerifier);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record AssignRoleRequest(Guid RoleId);