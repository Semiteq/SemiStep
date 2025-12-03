namespace Core.Services;

public interface IActionRepository
{
	Result<ActionDefinition> GetActionDefinitionById(short id);

	Result<ActionDefinition> GetResultActionDefinitionByName(string name);

	Result<short> GetResultDefaultActionId();

	IReadOnlyDictionary<short, ActionDefinition> Actions { get; }
}
