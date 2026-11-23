using FluentResults;

namespace SemiStep.Core.Configuration.Loaders;

internal sealed class UnsupportedLocaleError(string value, string fileName, IReadOnlyList<string> acceptedValues)
	: Error($"Unsupported locale '{value}' in {fileName}; accepted: {string.Join(", ", acceptedValues)}")
{
	public string Value { get; } = value;

	public string FileName { get; } = fileName;

	public IReadOnlyList<string> AcceptedValues { get; } = acceptedValues;
}
