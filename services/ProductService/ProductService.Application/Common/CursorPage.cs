namespace ProductService.Application.Common;

/// <summary>API-facing cursor page shape — mirrors UserService's PagedResult&lt;T&gt;
/// but replaces Page/PageSize/TotalPages with NextCursor/HasMore, since keyset
/// pagination has no stable concept of "total pages" (the count can change between
/// requests without invalidating the cursor, unlike an offset).</summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);
