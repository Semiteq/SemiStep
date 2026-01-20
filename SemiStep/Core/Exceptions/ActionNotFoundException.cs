namespace Core.Exceptions;

public sealed class ActionNotFoundException : CoreException
{
	public string ActionKey { get; }

	public ActionNotFoundException(string actionKey)
		: base($"Action '{actionKey}' not found")
	{
		ActionKey = actionKey;
	}
}
