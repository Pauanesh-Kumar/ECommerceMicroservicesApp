using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentService.Application.Commands.ConfirmPayment;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Gateways;

public sealed class PaymentReconciliationOptions
{
    public int IntervalSeconds { get; set; } = 60;
    public int MinimumAgeSeconds { get; set; } = 120;
}

public sealed class PaymentReconciliationHostedService(
    IServiceScopeFactory scopes, IOptions<PaymentReconciliationOptions> options,
    ILogger<PaymentReconciliationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (settings.IntervalSeconds < 1 || settings.MinimumAgeSeconds < 30)
            throw new InvalidOperationException("Invalid payment reconciliation timing configuration.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ReconcileAsync(settings, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Payment reconciliation cycle failed"); }
            await Task.Delay(TimeSpan.FromSeconds(settings.IntervalSeconds), stoppingToken);
        }
    }

    private async Task ReconcileAsync(PaymentReconciliationOptions settings, CancellationToken ct)
    {
        await using var scanScope = scopes.CreateAsyncScope();
        var scanDb = scanScope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var cutoff = DateTime.UtcNow.AddSeconds(-settings.MinimumAgeSeconds);
        var ids = await scanDb.Payments.AsNoTracking()
            .Where(p => (p.Status == PaymentStatus.Initiating || p.Status == PaymentStatus.Pending) && p.CreatedAtUtc < cutoff)
            .OrderBy(p => p.CreatedAtUtc).Select(p => p.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            try { await ReconcileOneAsync(id, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogError(ex, "Could not reconcile payment {PaymentId}", id); }
        }
    }

    private async Task ReconcileOneAsync(Guid id, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var payment = await db.Payments.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (payment is null || payment.Status is not (PaymentStatus.Initiating or PaymentStatus.Pending)) return;
        var provider = scope.ServiceProvider.GetRequiredService<RazorpayReconciliationClient>();
        var result = await provider.FindAsync(payment.OrderId, payment.Amount, payment.Currency, ct);
        if (result is null)
        {
            logger.LogWarning("No exact provider order found for payment {PaymentId}; no automatic create or failure will be issued", id);
            return;
        }
        if (payment.Status == PaymentStatus.Initiating)
        {
            payment.SetProviderOrder(result.Id);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Reconciled provider order for payment {PaymentId}", id);
        }
        else if (payment.ProviderOrderId != result.Id)
            throw new InvalidDataException("Provider order ID conflicts with the stored payment.");

        if (result.CapturedPaymentId is not null)
        {
            var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
            await mediator.Send(new ConfirmPaymentCommand("reconciliation:" + result.CapturedPaymentId,
                result.Id, result.CapturedPaymentId, true, null), ct);
        }
    }
}
