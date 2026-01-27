using Config.Dto;

using Shared;

namespace Config.Models;

public sealed class ConfigContext
{
	public List<string> FilePaths { get; init; } = [];

	public List<ActionDto>? Actions { get; set; }

	public List<ColumnDto>? Columns { get; set; }

	public List<PropertyDto>? Properties { get; set; }

	public Dictionary<string, Dictionary<int, string>>? Groups { get; set; }

	public GridStyleOptionsDto? GridStyle { get; set; }

	public AppConfiguration? Configuration { get; set; }

	public List<ConfigError> Errors { get; } = [];

	public Dictionary<string, object> Metadata { get; } = [];

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
