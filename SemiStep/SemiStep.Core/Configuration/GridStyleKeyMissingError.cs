using FluentResults;

namespace SemiStep.Core.Configuration;

public sealed class GridStyleKeyMissingError(string section, string key)
	: Error($"Grid style '{section}.{key}' is missing or empty.")
{
	public string Section { get; } = section;

	public string Key { get; } = key;
}
