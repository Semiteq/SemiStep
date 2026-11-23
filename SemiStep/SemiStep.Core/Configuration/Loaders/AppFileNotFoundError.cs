using FluentResults;

namespace SemiStep.Core.Configuration.Loaders;

internal sealed class AppFileNotFoundError(string filePath)
	: Error($"Required config not found: {filePath}")
{
	public string FilePath { get; } = filePath;
}
