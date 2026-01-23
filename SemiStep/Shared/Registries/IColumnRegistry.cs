using Shared.Entities;

namespace Shared.Registries;

public interface IColumnRegistry
{
	ColumnDefinition GetColumn(string key);

	bool ColumnExists(string key);

	IReadOnlyList<ColumnDefinition> GetAll();
}
