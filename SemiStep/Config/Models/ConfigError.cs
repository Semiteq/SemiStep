namespace Config.Models;

public sealed class ConfigError(ErrorSeverity severity, string message, string? location = null)
{
	public ErrorSeverity Severity { get; } = severity;
	public string Message { get; } = message;
	public string? Location { get; } = location;
	public DateTime Timestamp { get; } = DateTime.UtcNow;

	public override string ToString()
	{
		var locationPart = Location != null ? $" at {Location}" : string.Empty;
		return $"[{Severity}] {Message}{locationPart}";
	}
}
