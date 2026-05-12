namespace SemiStep.UI.MessageService;

public sealed record MessageEntry(
	MessageSeverity Severity,
	string Message,
	string Source,
	DateTime Timestamp)
{
	internal const string StructuralSource = "Recipe";
	public bool IsStructural => Source == StructuralSource;
	public bool IsError => Severity == MessageSeverity.Error;
	public bool IsWarning => Severity == MessageSeverity.Warning;
	public bool IsInfo => Severity == MessageSeverity.Info;
}
