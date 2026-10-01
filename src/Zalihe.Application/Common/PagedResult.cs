namespace Zalihe.Application.Common;

/// <summary>One page of a list. Every list in the API is paged on the server.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
