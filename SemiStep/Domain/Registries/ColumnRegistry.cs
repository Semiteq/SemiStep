using Shared.Entities;
using Shared.Registries;

namespace Domain.Registries;

public sealed class ColumnRegistry : IColumnRegistry
{
	private readonly Dictionary<string, GridColumnDefinition> _columns = new(StringComparer.OrdinalIgnoreCase);

	public void Initialize(IReadOnlyDictionary<string, GridColumnDefinition> columns)
	{
		_columns.Clear();

		foreach (var (key, column) in columns)
		{
			_columns[key] = column;
		}
	}

	public GridColumnDefinition GetColumn(string key)
	{
		if (!_columns.TryGetValue(key, out var column))
		{
			throw new KeyNotFoundException($"Column with key '{key}' not found");
		}

		return column;
	}

	public bool ColumnExists(string key) => _columns.ContainsKey(key);

	public IReadOnlyList<GridColumnDefinition> GetAll() => _columns.Values.ToList();
}
