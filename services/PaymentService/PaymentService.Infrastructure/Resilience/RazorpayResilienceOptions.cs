using Microsoft.Extensions.Http.Resilience;

namespace PaymentService.Infrastructure.Resilience;

/// <summary>
/// Timeout and circuit-breaker settings for Razorpay. Automatic retries are disabled
/// because this client creates orders and a timed-out POST may have succeeded remotely.
/// </summary>
public static class RazorpayResilienceOptions
{
    public static void Configure(HttpStandardResilienceOptions options)
    {
        // This client creates provider orders; an ambiguous failure is not safe to retry.
        options.Retry.ShouldHandle = _ => ValueTask.FromResult(false);

        // ── Per-attempt timeout: real internet + a third-party API — give it more
        // room than an internal call before declaring the attempt dead.
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(8);

        // ── Total request timeout: this runs inside a Kafka consumer, not blocking a
        // live user request, so it can afford to wait longer overall than Cart→Order.
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);

        // ── Circuit breaker: lower MinimumThroughput (5) because payment volume per
        // instance is naturally lower than an internal service call — waiting for 10
        // samples could take too long to ever trip. Longer 60s sampling window and 30s
        // break duration reflect that a real Razorpay outage/rate-limit event doesn't
        // typically resolve in 15 seconds, so probing too eagerly just burns another
        // failed call (and possibly counts against the rate limit) for no benefit.
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
    }
}
