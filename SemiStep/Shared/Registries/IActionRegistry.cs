using Shared.Entities;

namespace Shared.Registries;

public interface IActionRegistry
{
	void Initialize(IReadOnlyDictionary<short, ActionDefinition> actions);
	ActionDefinition GetAction(short id);
	ActionDefinition GetActionByName(string name);
	bool ActionExists(short id);
	bool ActionExistsByName(string name);
	IReadOnlyList<ActionDefinition> GetAll();
}
