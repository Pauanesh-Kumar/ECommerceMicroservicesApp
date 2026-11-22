namespace CartService.Application.Common;

public sealed record CheckoutResult(bool Success, Guid? OrderId, string? Error);
