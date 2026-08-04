using FluentResults;

namespace SemiStep.Core.Configuration;

public sealed class GridStyleHexColorInvalidError(string section, string key, string value)
	: Error($"Grid style '{section}.{key}' has invalid hex color: '{value}'. Expected format: '#RRGGBB' or '#AARRGGBB'.")
{
	public string Section { get; } = section;

	public string Key { get; } = key;

	public string Value { get; } = value;
}
