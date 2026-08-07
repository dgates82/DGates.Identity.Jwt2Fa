namespace DGates.Identity.Jwt2Fa.Dtos;

/// <summary>A page of results from the admin user list endpoint.</summary>
public class PagedResultDto<T>
{
    /// <summary>The items on this page.</summary>
    public required IReadOnlyList<T> Items { get; set; }

    /// <summary>The total number of items across all pages.</summary>
    public int TotalCount { get; set; }

    /// <summary>The current page number, starting at 1.</summary>
    public int Page { get; set; }

    /// <summary>The number of items per page.</summary>
    public int PageSize { get; set; }
}
