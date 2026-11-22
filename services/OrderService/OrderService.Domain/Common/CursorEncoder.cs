using System.Text;

namespace OrderService.Domain.Common;

/// <summary>Same keyset/seek-method cursor encoding as ProductService.Domain.Common.CursorEncoder
/// — kept as an independent copy per service (no shared library dependency across services,
/// matching this codebase's existing convention of duplicating small cross-cutting pieces
/// rather than introducing a shared NuGet package prematurely).</summary>
public static class CursorEncoder
{
    public sealed record CursorKey(DateTime CreatedAtUtc, Guid Id);

    public static string Encode(DateTime createdAtUtc, Guid id)
    {
        var raw = $"{createdAtUtc.Ticks}|{id:D}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public static CursorKey? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|', 2);
            if (parts.Length != 2) return null;
            if (!long.TryParse(parts[0], out var ticks)) return null;
            if (!Guid.TryParse(parts[1], out var id)) return null;
            return new CursorKey(new DateTime(ticks, DateTimeKind.Utc), id);
        }
        catch { return null; }
    }
}
