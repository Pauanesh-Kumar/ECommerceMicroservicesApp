using ECommerce.Shared.Security;
using HotChocolate.Authorization;
using HotChocolate.Types;
using MediatR;
using UserService.Api.GraphQL.Common;
using UserService.Application.Features.Users.Commands.ChangePassword;
using UserService.Application.Features.Users.Commands.DeactivateUser;
using UserService.Application.Features.Users.Commands.RegisterUser;
using UserService.Application.Features.Users.Commands.UpdateUser;
using UserService.Application.Features.Users.Commands.AssignUserRole;

namespace UserService.Api.GraphQL.Users;

[ExtendObjectType(typeof(Mutation))]
public sealed class UserMutations
{
    public Task<RegisterUserPayload> RegisterUserAsync(
        RegisterUserInput input, ISender mediator, CancellationToken ct)
        => mediator.SendAndMapValueAsync(
            new RegisterUserCommand(input.Email, input.Password, input.FirstName, input.LastName, input.PhoneNumber, input.RoleId),
            onSuccess: v => new RegisterUserPayload(v.UserId, true, null, null),
            onFailure: err => new RegisterUserPayload(null, false, err.Code, err.Message),
            ct);

    [Authorize(Policy = "User")]
    public Task<UpdateUserPayload> UpdateUserAsync(
        Guid id, UpdateUserInput input, ISender mediator, CancellationToken ct)
        => mediator.SendAndMapValueAsync(
            new UpdateUserCommand(id, input.FirstName, input.LastName, input.PhoneNumber),
            onSuccess: v => new UpdateUserPayload(true, null, null),
            onFailure: err => new UpdateUserPayload(false, err.Code, err.Message),
            ct);

    [Authorize(Policy = "Admin")]
    public Task<DeactivateUserPayload> DeactivateUserAsync(
        Guid id, string? reason, ISender mediator, CancellationToken ct)
        => mediator.SendAndMapAsync(
            new DeactivateUserCommand(id, reason),
            onSuccess: () => new DeactivateUserPayload(true, null, null),
            onFailure: err => new DeactivateUserPayload(false, err.Code, err.Message),
            ct);

    [Authorize(Policy = "User")]
    public async Task<ChangePasswordPayload> ChangePasswordAsync(
        ChangePasswordInput input, ICurrentUserAccessor currentUser, ISender mediator, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            return new ChangePasswordPayload(false, "UNAUTHORIZED", "Unable to resolve current user.");

        return await mediator.SendAndMapAsync(
            new ChangePasswordCommand(userId, input.CurrentPassword, input.NewPassword),
            onSuccess: () => new ChangePasswordPayload(true, null, null),
            onFailure: err => new ChangePasswordPayload(false, err.Code, err.Message),
            ct);
    }

    [Authorize(Policy = "Admin")]
    public Task<AssignRolePayload> AssignUserRoleAsync(
        Guid id, Guid roleId, ISender mediator, CancellationToken ct)
        => mediator.SendAndMapAsync(
            new AssignUserRoleCommand(id, roleId),
            onSuccess: () => new AssignRolePayload(true, null, null),
            onFailure: err => new AssignRolePayload(false, err.Code, err.Message),
            ct);
}