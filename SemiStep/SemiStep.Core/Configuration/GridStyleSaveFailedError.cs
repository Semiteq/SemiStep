using FluentResults;

namespace SemiStep.Core.Configuration;

public sealed class GridStyleSaveFailedError(string fileName)
	: Error($"Failed to save {fileName}")
{
	public string FileName { get; } = fileName;
}
