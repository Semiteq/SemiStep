using Shared.Entities;

namespace Shared.Registries;

public interface IActionRegistry
{
	ActionDefinition GetAction(short id);

	ActionDefinition GetActionByName(string name);

	bool ActionExists(short id);

	bool ActionExistsByName(string name);

	IReadOnlyList<ActionDefinition> GetAll();
}
