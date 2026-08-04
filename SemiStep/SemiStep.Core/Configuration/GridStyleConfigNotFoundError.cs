using FluentResults;

namespace SemiStep.Core.Configuration;

public sealed class GridStyleConfigNotFoundError(string filePath)
	: Error($"Grid style config not found: {filePath}")
{
	public string FilePath { get; } = filePath;
}
