namespace PaymentService.Application.Interfaces;

public sealed record GatewayOrderResult(bool Success, string? ProviderOrderId, string? Error);

/// <summary>Delivery-port abstraction (DIP) — Application/Commands depend on this,
/// never on the concrete Razorpay SDK, exactly like IEmailSender/ISmsSender in
/// NotificationService. Swapping to Stripe/PayU later means adding one adapter class
/// in Infrastructure/Gateways.</summary>
public interface IPaymentGateway
{
    Task<GatewayOrderResult> CreateOrderAsync(Guid internalOrderId, decimal amount, string currency, CancellationToken ct = default);

    /// <summary>Verifies the client-submitted payment confirmation (checkout.js callback)
    /// OR a webhook payload — same HMAC-SHA256 signature scheme either way.</summary>
    bool VerifySignature(string payload, string signatureHeader);
}
