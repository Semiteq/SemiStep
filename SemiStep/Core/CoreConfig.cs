using Core.Entities;

namespace Core;

public class CoreConfig
{
	private const string IterationColumnName = "task";

	public readonly ColumnId IterationColumnId = new(IterationColumnName);
}
