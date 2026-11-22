using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace CartService.Infrastructure.Resilience;

/// <summary>
/// Tuning for the synchronous Cart → Order HTTP call (checkout). This is an INTERNAL,
/// same-cluster dependency on the request-response path a real user is waiting on, so
/// the priorities are: fail fast for the caller, recover quickly once Order is healthy
/// again, and never retry so aggressively that a struggling OrderService gets pushed
/// over rather than given room to recover.
///
/// Mirrors the same Microsoft.Extensions.Http.Resilience "standard handler" (retry +
/// circuit breaker + timeouts) already used in ApiGateway.HttpClientExtensions for the
/// User/Notification downstream clients — same package, same shape, tuned differently
/// per dependency instead of using one blanket setting for every HttpClient.
/// </summary>
public static class OrderServiceResilienceOptions
{
    public static void Configure(HttpStandardResilienceOptions options)
    {
        // ── Retry: 2 attempts, exponential backoff + jitter (jitter avoids every
        // Cart instance retrying in lockstep and creating a synchronized thundering
        // herd against Order right as it starts recovering). Only fires on the
        // library's default transient predicate — network failures, 5xx, 408 — never
        // on 4xx, since a 4xx from Order (e.g. validation failure) is not transient
        // and must not be retried.
        options.Retry.MaxRetryAttempts = 2;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.Retry.Delay = TimeSpan.FromMilliseconds(200);

        // ── Per-attempt timeout: a checkout call is user-facing — 3s per try is
        // already a long wait for a same-cluster hop; don't let a single hung
        // attempt eat the whole request budget.
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);

        // ── Total request timeout across all retries — the hard ceiling the calling
        // user actually experiences.
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);

        // ── Circuit breaker: opens once at least 10 calls have been sampled in a
        // rolling 30s window AND ≥50% of them failed — avoids flipping the breaker
        // from a handful of unlucky calls at low traffic. 15s break duration is short
        // deliberately: Order is internal infrastructure, so re-probing quickly once
        // it's likely recovered costs little and gets checkout working again sooner.
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 10;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
    }
}
