using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class UnsupportedPropertySystemTypeError(string systemType)
	: Error($"Unsupported property system type: {systemType}")
{
	public string SystemType { get; } = systemType;
}
