using FluentResults;

namespace SemiStep.Core.Configuration;

public sealed class GridStyleSectionMissingError(string section)
	: Error($"Grid style configuration is missing '{section}' section.")
{
	public string Section { get; } = section;
}
