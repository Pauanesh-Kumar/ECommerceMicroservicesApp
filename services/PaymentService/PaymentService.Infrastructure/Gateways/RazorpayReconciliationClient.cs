using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PaymentService.Infrastructure.Gateways;

public sealed record ReconciledProviderOrder(string Id, string? CapturedPaymentId);

public sealed class RazorpayReconciliationClient
{
    private const int PageSize = 100;
    private readonly HttpClient http;

    public RazorpayReconciliationClient(HttpClient http, IOptions<RazorpaySettings> options)
    {
        this.http = http;
        var settings = options.Value;
        http.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.KeyId}:{settings.KeySecret}")));
    }

    public async Task<ReconciledProviderOrder?> FindAsync(Guid orderId, decimal amount, string currency, CancellationToken ct)
    {
        var receipt = orderId.ToString();
        var expectedAmount = checked((long)(amount * 100));
        JsonElement? match = null;
        for (var skip = 0; skip < 10000; skip += PageSize)
        {
            using var response = await http.GetAsync($"orders?receipt={Uri.EscapeDataString(receipt)}&count={PageSize}&skip={skip}", ct);
            response.EnsureSuccessStatusCode();
            var page = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var items = page.GetProperty("items");
            if (items.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid provider orders page.");
            foreach (var item in items.EnumerateArray())
            {
                if (item.GetProperty("receipt").GetString() != receipt) continue;
                if (match.HasValue) throw new InvalidDataException("Multiple provider orders have the same receipt.");
                if (item.GetProperty("amount").GetInt64() != expectedAmount || item.GetProperty("currency").GetString() != currency)
                    throw new InvalidDataException("Provider order amount or currency does not match the payment.");
                match = item.Clone();
            }
            if (items.GetArrayLength() < PageSize) break;
            if (skip + PageSize >= 10000) throw new InvalidDataException("Provider order search exceeded the page limit.");
        }
        if (!match.HasValue) return null;
        var order = match.Value;
        var id = order.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("Provider order ID is missing.");
        var status = order.GetProperty("status").GetString();
        if (status is "created" or "attempted") return new(id, null);
        if (status != "paid") throw new InvalidDataException("Unexpected provider order status.");

        using var paymentsResponse = await http.GetAsync($"orders/{Uri.EscapeDataString(id)}/payments", ct);
        paymentsResponse.EnsureSuccessStatusCode();
        var payments = await paymentsResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        string? captured = null;
        foreach (var payment in payments.GetProperty("items").EnumerateArray())
        {
            if (payment.GetProperty("status").GetString() != "captured") continue;
            if (payment.GetProperty("order_id").GetString() != id || payment.GetProperty("amount").GetInt64() != expectedAmount || payment.GetProperty("currency").GetString() != currency)
                throw new InvalidDataException("Captured provider payment does not match the order.");
            var paymentId = payment.GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(paymentId) || captured is not null)
                throw new InvalidDataException("Expected exactly one captured provider payment.");
            captured = paymentId;
        }
        return new(id, captured ?? throw new InvalidDataException("Paid provider order has no captured payment."));
    }
}
