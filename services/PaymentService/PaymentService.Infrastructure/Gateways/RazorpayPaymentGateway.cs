using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentService.Application.Interfaces;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace PaymentService.Infrastructure.Gateways;

/// <summary>
/// Razorpay Orders API adapter. Flow: create a Razorpay "order" (their term for a
/// payment intent) server-side, hand razorpay_order_id + KeyId to the client to open
/// Razorpay Checkout, then verify the signature Razorpay returns/sends via webhook
/// before ever marking our own Payment as Succeeded. Never trust a client-reported
/// "payment succeeded" without signature verification.
/// Docs: https://razorpay.com/docs/api/orders/ and https://razorpay.com/docs/webhooks/
/// </summary>
public sealed class RazorpayPaymentGateway : IPaymentGateway
{
    private readonly HttpClient _http;
    private readonly RazorpaySettings _settings;
    private readonly ILogger<RazorpayPaymentGateway> _logger;

    public RazorpayPaymentGateway(HttpClient http, IOptions<RazorpaySettings> options, ILogger<RazorpayPaymentGateway> logger)
    {
        _settings = options.Value;
        _http = http;
        _http.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        var authBytes = Encoding.UTF8.GetBytes($"{_settings.KeyId}:{_settings.KeySecret}");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        _logger = logger;
    }

    public async Task<GatewayOrderResult> CreateOrderAsync(Guid internalOrderId, decimal amount, string currency, CancellationToken ct = default)
    {
        // Razorpay amounts are in the smallest currency unit (paise for INR).
        var amountInSubunits = (long)(amount * 100);
        var payload = new { amount = amountInSubunits, currency, receipt = internalOrderId.ToString(), payment_capture = 1 };

        try
        {
            using var response = await _http.PostAsJsonAsync("orders", payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Razorpay CreateOrder returned {Status} for internalOrderId={OrderId}", response.StatusCode, internalOrderId);
                return new GatewayOrderResult(false, null, $"Razorpay error {(int)response.StatusCode}");
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var providerOrderId = json.GetProperty("id").GetString();
            return new GatewayOrderResult(true, providerOrderId, null);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(ex, "Razorpay circuit is open for internalOrderId={OrderId}", internalOrderId);
            return new GatewayOrderResult(false, null, "Payment gateway is temporarily unavailable.");
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning(ex, "Razorpay call timed out for internalOrderId={OrderId}", internalOrderId);
            return new GatewayOrderResult(false, null, "Payment gateway did not respond in time.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Razorpay connection failed for internalOrderId={OrderId}", internalOrderId);
            return new GatewayOrderResult(false, null, "Payment gateway connection failed.");
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Razorpay call timed out for internalOrderId={OrderId}", internalOrderId);
            return new GatewayOrderResult(false, null, "Payment gateway did not respond in time.");
        }
    }

    /// <summary>
    /// HMAC-SHA256(payload, WebhookSecret) must equal the signature header, using
    /// constant-time comparison to avoid timing attacks.
    /// </summary>
    public bool VerifySignature(string payload, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader)) return false;

        var keyBytes = Encoding.UTF8.GetBytes(_settings.WebhookSecret);
        var computedHash = HMACSHA256.HashData(keyBytes, Encoding.UTF8.GetBytes(payload));
        var computedHex = Convert.ToHexStringLower(computedHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(signatureHeader.ToLowerInvariant()));
    }
}
