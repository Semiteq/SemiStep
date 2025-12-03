using Core.Reasons.Errors;

using FluentResults;

namespace Core.Services;

public sealed class ActionRepository : IActionRepository
{
	public ActionRepository(AppConfiguration configuration)
	{
		Actions = configuration.Actions ?? throw new ArgumentNullException(nameof(configuration));
	}

	public IReadOnlyDictionary<short, ActionDefinition> Actions { get; }

	public Result<ActionDefinition> GetActionDefinitionById(short id)
	{
		return Actions.TryGetValue(id, out var action)
			? action
			: new CoreActionNotFoundError(id);
	}

	public Result<ActionDefinition> GetResultActionDefinitionByName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
			return new CoreActionNameEmptyError();

		var action = Actions.Values.FirstOrDefault(a =>
			string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

		if (action != null)
			return Result.Ok(action);

		return new CoreActionNameNotFoundError(name);
	}

	public Result<short> GetResultDefaultActionId()
	{
		var first = Actions.Values.FirstOrDefault();
		return first is null
			? new CoreNoActionsInConfigError()
			: Result.Ok(first.Id);
	}
}
