namespace Core.Reasons.Errors;

public sealed class CoreActionNameNotFoundError : BilingualError
{
	public string ActionName { get; }

	public CoreActionNameNotFoundError(string actionName)
		: base(
			$"Action with name '{actionName}' not found",
			$"Действие с именем '{actionName}' не найдено")
	{
		ActionName = actionName;
	}
}
