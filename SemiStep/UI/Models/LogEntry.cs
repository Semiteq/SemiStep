namespace UI.Models;

public sealed record LogEntry(
	LogSeverity Severity,
	string Message,
	string Source,
	DateTime Timestamp)
{
	internal const string StructuralSource = "Recipe";
	public bool IsStructural => Source == StructuralSource;
	public bool IsError => Severity == LogSeverity.Error;
	public bool IsWarning => Severity == LogSeverity.Warning;
	public bool IsInfo => Severity == LogSeverity.Info;
}
