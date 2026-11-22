using Polly;

namespace ECommerce.Shared.Infrastructure.Resilience;

/// <summary>
/// Polly resilience policies: Retry + Circuit Breaker + Timeout
/// Retry: exponential backoff, 3 attempts (1s, 2s, 4s)
/// Circuit Breaker: 3 failures in 30s → opens for 30s
/// Timeout: 5 seconds per request
/// </summary>
public static class ResiliencyPolicies
{
    public static IAsyncPolicy<T> GetRetryPolicy<T>() where T : class
    {
        return Policy<T>
            .Handle<Exception>()
            .OrResult(r => r == null)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt) - 1),
                onRetry: (outcome, timeout, retryCount, context) =>
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Polly] Retry {retryCount} after {timeout.TotalSeconds}s");
                });
    }

    public static IAsyncPolicy<T> GetCircuitBreakerPolicy<T>() where T : class
    {
        return Policy<T>
            .Handle<Exception>()
            .OrResult(r => r == null)
            .CircuitBreakerAsync<T>(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, duration) =>
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Polly] Circuit breaker opened for {duration.TotalSeconds}s");
                },
                onReset: () =>
                {
                    System.Diagnostics.Debug.WriteLine("Circuit breaker reset");
                });
    }

    public static IAsyncPolicy<T> GetTimeoutPolicy<T>() where T : class
    {
        return Policy.TimeoutAsync<T>(TimeSpan.FromSeconds(5));
    }

    public static IAsyncPolicy<T> GetCombinedPolicy<T>() where T : class
    {
        var timeoutPolicy = GetTimeoutPolicy<T>();
        var retryPolicy = GetRetryPolicy<T>();
        var circuitBreakerPolicy = GetCircuitBreakerPolicy<T>();
        return Policy.WrapAsync(circuitBreakerPolicy, retryPolicy, timeoutPolicy);
    }
}