namespace UI.Models;

public sealed record LogEntry(
	LogSeverity Severity,
	string Message,
	string Source,
	DateTime Timestamp)
{
	public bool IsStructural => Source == StructuralSource;

	internal const string StructuralSource = "Recipe";
}
