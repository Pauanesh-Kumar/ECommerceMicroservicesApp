using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace ECommerce.Tests.Integration;

internal static class Program
{
    public static async Task<int> Main()
    {
        await using var test = new SagaTest();
        try
        {
            await test.RunAsync();
            Console.WriteLine("All isolated checkout saga scenarios passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            await test.PrintDiagnosticsAsync();
            return 1;
        }
    }
}

internal sealed partial class SagaTest : IAsyncDisposable
{
    private const string JwtKey = "isolated-integration-test-signing-key-not-for-production";
    private const string WebhookSecret = "isolated-webhook-secret";
    private readonly string root = FindRoot();
    private readonly string project = "ecommerce-saga-test-" + Guid.NewGuid().ToString("N")[..12];
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ecommerce-saga-test-" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, int> ports = new();
    private readonly Dictionary<string, string> environment = new();
    private readonly Dictionary<string, Dictionary<string, string>> serviceEnvironments = new();
    private readonly Dictionary<string, Process> processes = new();
    private readonly Dictionary<string, Task[]> logTasks = new();
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly ProviderStub provider = new();
    private bool composeStarted;
    private bool kafkaPaused;

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "tests", "ECommerce.Tests.Integration", "docker-compose.saga-test.yml"))) return dir.FullName;
        throw new DirectoryNotFoundException("Run from the repository root or a subdirectory.");
    }

    public async Task RunAsync()
    {
        Directory.CreateDirectory(directory);
        foreach (var name in new[] { "KAFKA", "POSTGRES", "MONGO", "REDIS", "PRODUCT", "CART", "ORDER", "PAYMENT", "ANALYTICS" }) ports[name] = FreePort();
        foreach (System.Collections.DictionaryEntry item in Environment.GetEnvironmentVariables())
        {
            var key = (string)item.Key;
            if (!new[] { "ConnectionStrings__", "Kafka__", "Redis__", "Jwt__", "Razorpay__", "Services__", "OpenTelemetry__" }.Any(key.StartsWith))
                environment[key] = (string)item.Value!;
        }
        foreach (var name in new[] { "KAFKA", "POSTGRES", "MONGO", "REDIS" }) environment["FLOW_" + name + "_PORT"] = ports[name].ToString();
        foreach (var service in new[] { "Product", "Cart", "Order", "Payment", "Analytics" })
            await CommandAsync("dotnet", ["build", ProjectPath(service), "--nologo", "-v", "quiet"], environment, root, 180);
        composeStarted = true;
        await ComposeAsync("up", "-d", "--wait", "--wait-timeout", "180");
        await ComposeAsync("exec", "-T", "mongo", "mongosh", "--quiet", "--eval", "rs.initiate({_id:'flow-rs',members:[{_id:0,host:'localhost:27017'}]})");
        await EventuallyAsync("MongoDB replica-set primary", async () => (await MongoAsync("print(db.hello().isWritablePrimary)")).Trim() == "true");
        foreach (var database in new[] { "orderdb", "paymentdb", "analyticsdb" })
            await ComposeAsync("exec", "-T", "postgres", "createdb", "-U", "flowtest", database);
        foreach (var topic in new[] { "order-created", "stock-release", "product-stock", "payment-requested", "payment-status", "order-confirmed", "order-cancelled" })
            await ComposeAsync("exec", "-T", "kafka", "kafka-topics", "--bootstrap-server", "kafka:29092", "--create", "--if-not-exists", "--topic", topic, "--partitions", "1", "--replication-factor", "1");
        await provider.StartAsync();
        foreach (var service in new[] { "Product", "Order", "Payment", "Cart", "Analytics" })
        {
            var vars = new Dictionary<string, string>(environment)
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development", ["Jwt__Key"] = JwtKey, ["Jwt__Issuer"] = "userservice", ["Jwt__Audience"] = "ecommerce-api",
                ["Kafka__BootstrapServers"] = $"127.0.0.1:{ports["KAFKA"]}", ["Redis__ConnectionString"] = $"127.0.0.1:{ports["REDIS"]}",
                ["ASPNETCORE_URLS"] = Url(service)
            };
            var pg = $"Host=127.0.0.1;Port={ports["POSTGRES"]};Username=flowtest;Password=local-test-only;Database=";
            var mongo = $"mongodb://127.0.0.1:{ports["MONGO"]}/?directConnection=true";
            switch (service)
            {
                case "Product": vars["ConnectionStrings__ProductServiceDb"] = mongo; break;
                case "Order": vars["ConnectionStrings__OrderServiceDb"] = pg + "orderdb"; break;
                case "Cart": vars["Services__OrderServiceBaseUrl"] = Url("Order"); break;
                case "Analytics": vars["ConnectionStrings__AnalyticsServiceDb"] = mongo; vars["ConnectionStrings__AnalyticsReportingDb"] = pg + "analyticsdb"; break;
                case "Payment":
                    vars["ConnectionStrings__PaymentServiceDb"] = pg + "paymentdb";
                    vars["Razorpay__BaseUrl"] = provider.Url + "/v1";
                    vars["Razorpay__KeyId"] = "test-key"; vars["Razorpay__KeySecret"] = "test-key-secret"; vars["Razorpay__WebhookSecret"] = WebhookSecret;
                    vars["PaymentReconciliation__IntervalSeconds"] = "2"; vars["PaymentReconciliation__MinimumAgeSeconds"] = "30";
                    break;
            }
            serviceEnvironments[service] = vars;
            await StartServiceAsync(service);
        }
        await ScenariosAsync();
    }

    private string ProjectPath(string service) => Path.Combine(root, "services", service + "Service", service + "Service.API", service + "Service.API.csproj");
    private string Url(string service) => $"http://127.0.0.1:{ports[service.ToUpperInvariant()]}";
    private static int FreePort() { var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); try { return ((IPEndPoint)listener.LocalEndpoint).Port; } finally { listener.Stop(); } }

    private async Task StartServiceAsync(string service)
    {
        var dll = Path.Combine(root, "services", service + "Service", service + "Service.API", "bin", "Debug", "net10.0", service + "Service.API.dll");
        var info = StartInfo("dotnet", [dll], serviceEnvironments[service], directory);
        var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start " + service);
        processes[service] = process;
        logTasks[service] = [CopyLogAsync(process.StandardOutput, Path.Combine(directory, service + ".log")), CopyLogAsync(process.StandardError, Path.Combine(directory, service + ".error.log"))];
        await EventuallyAsync(service + " health", async () => (await RequestAsync(service, "/health/live")).Status == 200);
    }

    private static async Task CopyLogAsync(StreamReader reader, string path)
    {
        await using var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        await using var writer = new StreamWriter(file) { AutoFlush = true };
        while (await reader.ReadLineAsync() is { } line) await writer.WriteLineAsync(line);
    }

    private async Task StopServiceAsync(string service)
    {
        var process = processes[service];
        if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        await Task.WhenAll(logTasks[service]);
        logTasks.Remove(service);
        process.Dispose();
        processes.Remove(service);
    }

    private static ProcessStartInfo StartInfo(string executable, IEnumerable<string> args, Dictionary<string, string> env, string cwd)
    {
        var info = new ProcessStartInfo(executable) { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        info.Environment.Clear();
        foreach (var pair in env) info.Environment[pair.Key] = pair.Value;
        return info;
    }

    private static async Task<string> CommandAsync(string executable, IEnumerable<string> args, Dictionary<string, string> env, string cwd, int timeout = 120)
    {
        using var process = Process.Start(StartInfo(executable, args, env, cwd)) ?? throw new InvalidOperationException("Could not start " + executable);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
        try { await process.WaitForExitAsync(cancellation.Token); }
        catch (OperationCanceledException) { process.Kill(true); await process.WaitForExitAsync(); throw new TimeoutException($"{executable} timed out."); }
        var output = await stdout;
        var error = await stderr;
        if (process.ExitCode != 0) throw new InvalidOperationException($"{executable} failed ({process.ExitCode}): {error}\n{output}");
        return output.Trim();
    }

    private Task<string> ComposeAsync(params string[] args) => CommandAsync("docker", ["compose", "-p", project, "-f", Path.Combine(root, "tests", "ECommerce.Tests.Integration", "docker-compose.saga-test.yml"), .. args], environment, root, 240);
    private Task<string> MongoAsync(string js) => ComposeAsync("exec", "-T", "mongo", "mongosh", "--quiet", "--eval", js);
    private Task<string> SqlAsync(string db, string sql) => ComposeAsync("exec", "-T", "postgres", "psql", "-U", "flowtest", "-d", db, "-v", "ON_ERROR_STOP=1", "-At", "-c", sql);
    private async Task PauseKafkaAsync() { await ComposeAsync("pause", "kafka"); kafkaPaused = true; }
    private async Task UnpauseKafkaAsync() { await ComposeAsync("unpause", "kafka"); kafkaPaused = false; }

    private async Task<(int Status, JsonNode? Body)> RequestAsync(string service, string path, object? body = null, Dictionary<string, string>? headers = null)
    {
        using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, Url(service) + path);
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (headers is not null) foreach (var pair in headers) request.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
        using var response = await http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        JsonNode? result = null;
        if (response.Content.Headers.ContentType?.MediaType == "application/json" && !string.IsNullOrEmpty(text)) result = JsonNode.Parse(text);
        return ((int)response.StatusCode, result);
    }

    private static async Task EventuallyAsync(string description, Func<Task<bool>> condition, int seconds = 90)
    {
        var deadline = Stopwatch.StartNew();
        Exception? last = null;
        while (deadline.Elapsed < TimeSpan.FromSeconds(seconds))
        {
            try { if (await condition()) return; }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException) { last = ex; }
            await Task.Delay(1000);
        }
        throw new TimeoutException("Timed out waiting for " + description, last);
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static string Jwt(string user)
    {
        static string Encode(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var body = Encode(JsonSerializer.SerializeToUtf8Bytes(new { sub = user, role = "Admin", iss = "userservice", aud = "ecommerce-api", exp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds() }));
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(JwtKey), Encoding.UTF8.GetBytes(header + "." + body));
        return header + "." + body + "." + Encode(signature);
    }

    private async Task<int> WebhookAsync(string type, string eventId, string providerOrder, string providerPayment)
    {
        var body = new { @event = type, payload = new { payment = new { entity = new { id = providerPayment, order_id = providerOrder } } } };
        var signature = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(WebhookSecret), JsonSerializer.SerializeToUtf8Bytes(body)));
        return (await RequestAsync("Payment", "/api/payments/webhooks/razorpay", body, new() { ["X-Razorpay-Signature"] = signature, ["X-Razorpay-Event-Id"] = eventId })).Status;
    }

    public async Task PrintDiagnosticsAsync()
    {
        if (composeStarted) try { Console.Error.WriteLine(await ComposeAsync("logs", "--tail", "30", "kafka")); } catch (Exception ex) { Console.Error.WriteLine("Kafka diagnostics failed: " + ex.Message); }
        if (Directory.Exists(directory)) foreach (var file in Directory.GetFiles(directory, "*.log"))
        {
            try
            {
                await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                var lines = (await reader.ReadToEndAsync()).Split('\n').TakeLast(35);
                Console.Error.WriteLine($"--- {Path.GetFileName(file)} ---\n{string.Join(Environment.NewLine, lines)}");
            }
            catch (IOException ex) { Console.Error.WriteLine(ex.Message); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var process in processes.Values) if (!process.HasExited) process.Kill(true);
        foreach (var process in processes.Values) await process.WaitForExitAsync();
        await Task.WhenAll(logTasks.Values.SelectMany(tasks => tasks));
        foreach (var process in processes.Values) process.Dispose();
        await provider.DisposeAsync();
        http.Dispose();
        if (composeStarted)
        {
            if (kafkaPaused) await UnpauseKafkaAsync();
            await ComposeAsync("down", "--remove-orphans");
        }
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
