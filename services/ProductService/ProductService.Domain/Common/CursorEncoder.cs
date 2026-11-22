using System.Text;

namespace ProductService.Domain.Common;

/// <summary>
/// Keyset/cursor pagination helper. The cursor is an opaque, base64-encoded composite
/// key of (CreatedAtUtc, Id) — NOT an offset. This is the "seek method": every page
/// query is `WHERE (CreatedAtUtc, Id) > (@cursorCreatedAt, @cursorId) ORDER BY
/// CreatedAtUtc, Id LIMIT n`, which is O(log n) via the index on every page (unlike
/// OFFSET/SKIP pagination, which degrades linearly as the offset grows and is also
/// unstable under concurrent inserts/deletes shifting the "page" underneath the caller).
/// Guid.NewGuid() is NOT time-ordered in .NET, so CreatedAtUtc is the primary sort key
/// and Id is only the tiebreaker for rows with an identical timestamp.
/// </summary>
public static class CursorEncoder
{
    public sealed record CursorKey(DateTime CreatedAtUtc, Guid Id);

    public static string Encode(DateTime createdAtUtc, Guid id)
    {
        var raw = $"{createdAtUtc.Ticks}|{id:D}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public static string Encode(CursorKey key) => Encode(key.CreatedAtUtc, key.Id);

    /// <returns>null if the cursor is missing/malformed — callers should treat that as
    /// "first page" rather than throwing, since cursors are opaque to clients and a
    /// stale/tampered cursor must never 500 the endpoint.</returns>
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
        catch
        {
            return null;
        }
    }
}
