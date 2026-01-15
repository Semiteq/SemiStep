using Core.ConfigImport.Dto;

namespace Config.Models;

public sealed class ConfigContext
{
	public List<string> FilePaths { get; init; } = new();

	public Dictionary<string, object> RawData { get; set; } = new();

	public ConfigRootDto? ParsedConfig { get; set; }

	public List<ConfigError> Errors { get; } = new();

	public Dictionary<string, object> Metadata { get; } = new();

	public bool HasErrors => Errors.Any(e => e.Severity == ErrorSeverity.Error);

	public bool HasWarnings => Errors.Any(e => e.Severity == ErrorSeverity.Warning);

	public void AddError(string message, string? location = null)
	{
		Errors.Add(new ConfigError(ErrorSeverity.Error, message, location));
	}

	public void AddWarning(string message, string? location = null)
	{
		Errors.Add(new ConfigError(ErrorSeverity.Warning, message, location));
	}

	public void AddInfo(string message, string? location = null)
	{
		Errors.Add(new ConfigError(ErrorSeverity.Info, message, location));
	}
}
