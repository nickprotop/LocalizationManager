namespace LrmCloud.Web.Models;

/// <summary>
/// Represents the state of a data grid for server-side data loading.
/// </summary>
public class GridState<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; } = 50;
    public ICollection<SortDefinition<T>> SortDefinitions { get; set; } = new List<SortDefinition<T>>();
}

/// <summary>
/// Represents the result of server-side data loading.
/// </summary>
public class GridData<T>
{
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
    public int TotalItems { get; set; }
}

/// <summary>
/// Sort definition for grid columns.
/// </summary>
public class SortDefinition<T>
{
    public string? SortBy { get; set; }
    public bool Descending { get; set; }
}
