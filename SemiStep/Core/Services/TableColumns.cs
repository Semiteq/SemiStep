namespace Core.Services;

/// <summary>
/// Wrapper for column definitions to maintain compatibility.
/// </summary>
public sealed class TableColumns
{
	private readonly IReadOnlyList<ColumnDefinition> _columns;

	public TableColumns(IReadOnlyList<ColumnDefinition> columns)
	{
		_columns = columns ?? throw new ArgumentNullException(nameof(columns));
	}

	public IEnumerable<ColumnDefinition> GetColumns() => _columns;
}
