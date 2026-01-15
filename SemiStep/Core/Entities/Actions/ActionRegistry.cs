using System.Collections.Immutable;

using Core.Properties.Contracts;

namespace Core.Entities.Actions;

internal class ActionRegistry
{
	private readonly IReadOnlyList<ActionDefinition> _actionDefinitions;

	internal ActionRegistry(IReadOnlyList<ActionDefinition> actionDefinitions)
	{
		_actionDefinitions = actionDefinitions;
	}
}
