using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

var keyId = Environment.GetEnvironmentVariable("Razorpay__KeyId");
var keySecret = Environment.GetEnvironmentVariable("Razorpay__KeySecret");
if (string.IsNullOrWhiteSpace(keyId) || !keyId.StartsWith("rzp_test_", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(keySecret))
{
    Console.Error.WriteLine("Configure Razorpay__KeyId and Razorpay__KeySecret locally with valid Razorpay test-mode credentials.");
    return 1;
}

using var http = new HttpClient { BaseAddress = new Uri("https://api.razorpay.com/v1/"), Timeout = TimeSpan.FromSeconds(20) };
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}")));
try
{
    using var check = await http.GetAsync("orders?count=1");
    if (!check.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Razorpay test-mode authentication failed with HTTP {(int)check.StatusCode}.");
        return 1;
    }
    Console.WriteLine("Razorpay test-mode Orders API authentication succeeded.");
    if (!args.Contains("--create-order", StringComparer.Ordinal))
    {
        Console.WriteLine("No order was created. Pass --create-order to test order creation in the sandbox.");
        Console.WriteLine("This check does not capture a payment or validate real webhook delivery.");
        return 0;
    }

    var receipt = Guid.NewGuid().ToString();
    Console.WriteLine($"Sandbox test receipt: {receipt}; retain it to reconcile an ambiguous response.");
    using var response = await http.PostAsJsonAsync("orders", new { amount = 100, currency = "INR", receipt });
    if (!response.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Sandbox order creation returned HTTP {(int)response.StatusCode}; check Razorpay before retrying an ambiguous request.");
        return 1;
    }
    var created = await response.Content.ReadFromJsonAsync<JsonElement>();
    var providerId = created.GetProperty("id").GetString();
    if (string.IsNullOrWhiteSpace(providerId)) throw new InvalidDataException("Provider order ID missing from response.");
    using var fetched = await http.GetAsync($"orders/{Uri.EscapeDataString(providerId)}");
    if (!fetched.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Sandbox order verification returned HTTP {(int)fetched.StatusCode}.");
        return 1;
    }
    var order = await fetched.Content.ReadFromJsonAsync<JsonElement>();
    if (order.GetProperty("receipt").GetString() != receipt || order.GetProperty("currency").GetString() != "INR" || order.GetProperty("amount").GetInt64() != 100)
        throw new InvalidDataException("Provider order did not match the test receipt, currency and amount.");
    Console.WriteLine("Razorpay sandbox order creation and read-back succeeded. No payment was captured.");
    Console.WriteLine("Real checkout, webhook delivery and application reconciliation still require a configured sandbox and reachable HTTPS endpoint.");
    return 0;
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Razorpay connection failed: {ex.GetType().Name}. Investigate the provider before retrying any order creation.");
    return 1;
}
catch (TaskCanceledException)
{
    Console.Error.WriteLine("Razorpay request timed out. Investigate the provider before retrying any order creation.");
    return 1;
}
