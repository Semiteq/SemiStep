using FluentResults;

namespace SemiStep.Core.Configuration.Loaders;

internal sealed class MissingKeyError(string key, string fileName)
	: Error($"Missing required key '{key}' in {fileName}")
{
	public string Key { get; } = key;

	public string FileName { get; } = fileName;
}
