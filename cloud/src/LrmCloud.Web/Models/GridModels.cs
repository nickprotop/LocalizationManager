namespace LrmCloud.Web.Models;

/// <summary>
/// Represents the state of a data grid for server-side data loading.
/// </summary>
public class GridState<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; } = 50;
    public ICollection<SortDefinition<T>> SortDefinitions { get; set; } = new List<SortDefinition<T>>();
    public List<FilterDefinition>? Filters { get; set; }
    public string? SearchText { get; set; }
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

/// <summary>
/// Filter definition for grid columns.
/// </summary>
public class FilterDefinition
{
    public string Property { get; set; } = string.Empty;
    public string Operator { get; set; } = "contains"; // contains, equals, startswith, etc.
    public object? Value { get; set; }
    public string LogicalOperator { get; set; } = "AND"; // AND, OR
    public object? SecondValue { get; set; }
    public string? SecondOperator { get; set; }
}
