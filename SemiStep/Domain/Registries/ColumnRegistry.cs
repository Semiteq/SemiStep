using Shared.Entities;
using Shared.Registries;

namespace Domain.Registries;

public sealed class ColumnRegistry : IColumnRegistry
{
	private readonly Dictionary<string, ColumnDefinition> _columns = new(StringComparer.OrdinalIgnoreCase);

	public void Initialize(IReadOnlyDictionary<string, ColumnDefinition> columns)
	{
		_columns.Clear();

		foreach (var (key, column) in columns)
		{
			_columns[key] = column;
		}
	}

	public ColumnDefinition GetColumn(string key)
	{
		if (!_columns.TryGetValue(key, out var column))
		{
			throw new KeyNotFoundException($"Column with key '{key}' not found");
		}

		return column;
	}

	public bool ColumnExists(string key) => _columns.ContainsKey(key);

	public IReadOnlyList<ColumnDefinition> GetAll() => _columns.Values.ToList();
}
