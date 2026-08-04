using FluentResults;

namespace SemiStep.Core.Configuration;

public sealed class GridStyleLoadFailedError(string fileName)
	: Error($"Failed to load {fileName}")
{
	public string FileName { get; } = fileName;
}
