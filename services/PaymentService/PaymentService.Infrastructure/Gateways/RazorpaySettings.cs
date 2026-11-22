namespace PaymentService.Infrastructure.Gateways;

public sealed class RazorpaySettings
{
    public const string SectionName = "Razorpay";
    public string KeyId { get; init; } = default!;
    public string KeySecret { get; init; } = default!;
    public string WebhookSecret { get; init; } = default!;
    /// <summary>Use Razorpay's official TEST mode key pair (rzp_test_...) for manual/
    /// integration testing — no real money moves. See Razorpay Dashboard → Settings →
    /// API Keys → Test Mode.</summary>
    public string BaseUrl { get; init; } = "https://api.razorpay.com/v1";
}
