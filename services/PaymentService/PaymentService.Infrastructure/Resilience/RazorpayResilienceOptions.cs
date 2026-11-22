using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace PaymentService.Infrastructure.Resilience;

/// <summary>
/// Tuning for the Payment → Razorpay HTTP call. Razorpay is a THIRD-PARTY, rate-limited,
/// public-internet dependency called from a background Kafka consumer (not a live user
/// request) — so the priorities invert relative to OrderServiceResilienceOptions:
/// tolerate more latency per attempt (real internet round trip, not same-cluster), retry
/// less aggressively (every retry is a real request against someone else's rate limit,
/// and each one is a POST that creates a resource — see the idempotency caveat below),
/// and give the circuit a longer break before re-probing (an external outage rarely
/// clears in 15 seconds the way a same-cluster blip might).
/// </summary>
public static class RazorpayResilienceOptions
{
    public static void Configure(HttpStandardResilienceOptions options)
    {
        // ── Retry: only 2 attempts. POST /orders is NOT naturally idempotent on
        // Razorpay's side — if the first attempt actually succeeded but the response
        // was lost (timeout), a retry can create a SECOND Razorpay order for the same
        // internal OrderId. We pass `receipt = internalOrderId` on every create (see
        // RazorpayPaymentGateway.CreateOrderAsync) specifically so this is detectable/
        // reconcilable after the fact — for a stricter guarantee, add a
        // GET /orders?receipt={id} existence check before retrying, or wrap the call
        // with Razorpay's idempotency-key support if enabled on your account. Kept as
        // a documented follow-up here rather than implemented, to avoid adding an
        // extra network round-trip to every payment attempt by default.
        options.Retry.MaxRetryAttempts = 2;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.Retry.Delay = TimeSpan.FromSeconds(1);

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
