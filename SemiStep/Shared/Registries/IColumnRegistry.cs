using Shared.Entities;

namespace Shared.Registries;

public interface IColumnRegistry
{
	void Initialize(IReadOnlyDictionary<string, ColumnDefinition> columns);
	ColumnDefinition GetColumn(string key);
	bool ColumnExists(string key);
	IReadOnlyList<ColumnDefinition> GetAll();
}
