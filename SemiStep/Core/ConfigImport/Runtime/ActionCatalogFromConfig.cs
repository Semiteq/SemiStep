using Core.Definitions;
using Core.Definitions.Contracts;

using FluentResults;

namespace Core.ConfigImport.Runtime;

internal sealed class ActionCatalogFromConfig : IActionCatalog
{
	private readonly IReadOnlyDictionary<string, ActionDefinition> _byKey;

	internal ActionCatalogFromConfig(IReadOnlyDictionary<string, ActionDefinition> byKey)
	{
		_byKey = byKey ?? throw new ArgumentNullException(nameof(byKey));
	}

	public Result<ActionDefinition> GetByKey(string actionKey)
	{
		if (string.IsNullOrWhiteSpace(actionKey))
		{
			return Result.Fail("ActionKey is empty.");
		}

		return _byKey.TryGetValue(actionKey, out var def)
			? Result.Ok(def)
			: Result.Fail("ActionKey not found.");
	}
}
