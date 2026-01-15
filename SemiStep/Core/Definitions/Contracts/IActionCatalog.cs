using FluentResults;

namespace Core.Definitions.Contracts;

internal interface IActionCatalog
{
	Result<ActionDefinition> GetByKey(string actionKey);
}
