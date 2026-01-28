using Shared.Entities;

namespace Shared.Registries;

public interface IActionRegistry
{
	void Initialize(IReadOnlyDictionary<int, ActionDefinition> actions);
	ActionDefinition GetAction(int id);
	ActionDefinition GetActionByName(string name);
	bool ActionExists(short id);
	bool ActionExistsByName(string name);
	IReadOnlyList<ActionDefinition> GetAll();
}
