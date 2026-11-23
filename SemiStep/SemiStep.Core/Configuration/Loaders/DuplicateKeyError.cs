using FluentResults;

namespace SemiStep.Core.Configuration.Loaders;

internal sealed class DuplicateKeyError(string key, string firstFileName, string repeatFileName)
	: Error($"Duplicate key '{key}' in {repeatFileName}; first defined in {firstFileName}")
{
	public string Key { get; } = key;

	public string FirstFileName { get; } = firstFileName;

	public string RepeatFileName { get; } = repeatFileName;
}
