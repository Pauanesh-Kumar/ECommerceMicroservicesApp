namespace PaymentService.Domain.Entities;

public enum PaymentStatus { Pending, Succeeded, Failed }

public sealed class Payment
{
    private Payment() { } // EF Core

    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Unique index — this IS the idempotency guard for "don't charge the same
    /// order twice", not just a business FK.</summary>
    public Guid OrderId { get; private set; }

    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "INR";
    public PaymentStatus Status { get; private set; }
    public string Provider { get; private set; } = "Razorpay";
    public string? ProviderOrderId { get; private set; }
    public string? ProviderPaymentId { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; private set; }

    public static Payment CreatePending(Guid orderId, Guid userId, decimal amount, string currency, string providerOrderId) => new()
    {
        OrderId = orderId, UserId = userId, Amount = amount, Currency = currency,
        Status = PaymentStatus.Pending, ProviderOrderId = providerOrderId
    };

    public void MarkSucceeded(string providerPaymentId)
    {
        Status = PaymentStatus.Succeeded;
        ProviderPaymentId = providerPaymentId;
        ProcessedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        Status = PaymentStatus.Failed;
        FailureReason = reason;
        ProcessedAtUtc = DateTime.UtcNow;
    }
}
