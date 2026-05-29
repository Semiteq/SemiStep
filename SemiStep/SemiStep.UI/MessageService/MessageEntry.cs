namespace SemiStep.UI.MessageService;

public sealed record MessageEntry(
	MessageSeverity Severity,
	string Message)
{
	public bool IsError => Severity == MessageSeverity.Error;
	public bool IsWarning => Severity == MessageSeverity.Warning;
}
