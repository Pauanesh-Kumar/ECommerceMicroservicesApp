using MediatR;

namespace UserService.Api.GraphQL.Common;

/// <summary>
/// Strategy-pattern dispatch: onSuccess/onFailure are interchangeable mapping
/// strategies for turning a Result into a GraphQL payload. Every resolver reuses
/// this instead of re-writing "send then match" — a new mutation calls this,
/// it never edits this file (Open/Closed).
///
/// Two distinct method names (not two overloads of the same name) — same-name
/// generic overloads differing only by arity are fragile to resolve once lambda
/// arguments are involved; explicit names remove any ambiguity for the compiler
/// and for readers.
/// </summary>
public static class MediatorResultExtensions
{
    /// <summary>For commands returning the non-generic Result (success/failure, no value).</summary>
    public static async Task<TPayload> SendAndMapAsync<TPayload>(
        this ISender mediator,
        IRequest<DomainCommon.Result> command,
        Func<TPayload> onSuccess,
        Func<DomainErrors.Error, TPayload> onFailure,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Match(onSuccess, onFailure);
    }

    /// <summary>For commands returning Result&lt;TValue&gt; (success carries a value).</summary>
    public static async Task<TPayload> SendAndMapValueAsync<TValue, TPayload>(
        this ISender mediator,
        IRequest<DomainCommon.Result<TValue>> command,
        Func<TValue, TPayload> onSuccess,
        Func<DomainErrors.Error, TPayload> onFailure,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Match(onSuccess, onFailure);
    }
}