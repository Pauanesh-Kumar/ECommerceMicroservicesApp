using System.Text;

namespace PaymentService.Domain.Common;

/// <summary>Same keyset/seek-method cursor encoding as the sibling services — see
/// ProductService.Domain.Common.CursorEncoder for the full rationale.</summary>
public static class CursorEncoder
{
    public sealed record CursorKey(DateTime CreatedAtUtc, Guid Id);

    public static string Encode(DateTime createdAtUtc, Guid id)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{createdAtUtc.Ticks}|{id:D}"));

    public static CursorKey? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|', 2);
            if (parts.Length != 2 || !long.TryParse(parts[0], out var ticks) || !Guid.TryParse(parts[1], out var id)) return null;
            return new CursorKey(new DateTime(ticks, DateTimeKind.Utc), id);
        }
        catch { return null; }
    }
}
