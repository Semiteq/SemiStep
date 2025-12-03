namespace Core.Reasons.Errors;

public sealed class CoreNoActionsInConfigError : BilingualError
{
	public CoreNoActionsInConfigError()
		: base(
			"No actions defined in configuration",
			"В конфигурации не определены действия")
	{
	}
}
