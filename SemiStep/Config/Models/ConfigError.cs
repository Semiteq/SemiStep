namespace SemiStep.Config.Models;

/// <summary>
/// Представляет ошибку или предупреждение конфигурации
/// </summary>
public sealed class ConfigError
{
	public ErrorSeverity Severity { get; }
	public string Message { get; }
	public string? Location { get; }
	public DateTime Timestamp { get; }

	public ConfigError(ErrorSeverity severity, string message, string? location = null)
	{
		Severity = severity;
		Message = message;
		Location = location;
		Timestamp = DateTime.UtcNow;
	}

	public override string ToString()
	{
		var locationPart = Location != null ? $" at {Location}" : string.Empty;
		return $"[{Severity}] {Message}{locationPart}";
	}
}
