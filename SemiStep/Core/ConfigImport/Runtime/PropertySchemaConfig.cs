using Core.Definitions;
using Core.Definitions.Contracts;
using Core.Properties.Contracts;

using FluentResults;

namespace Core.ConfigImport.Runtime;

internal sealed class PropertySchemaFromConfig : IPropertySchema
{
	private readonly IReadOnlyDictionary<(string ActionKey, string ColumnId), IPropertyDefinition> _definitions;

	internal PropertySchemaFromConfig(IReadOnlyDictionary<(string ActionKey, string ColumnId), IPropertyDefinition> definitions)
	{
		_definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
	}

	public Result<IPropertyDefinition> GetDefinition(ActionDefinition action, string columnId)
	{
		if (action is null)
			throw new ArgumentNullException(nameof(action));
		if (string.IsNullOrWhiteSpace(columnId))
			return Result.Fail("ColumnId is empty.");

		return _definitions.TryGetValue((action.Key, columnId), out var definition)
			? Result.Ok(definition)
			: Result.Fail("ColumnId not found in action schema.");
	}
}
