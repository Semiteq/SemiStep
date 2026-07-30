using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class PropertyValueParseError(string rawValue, string targetType)
	: Error($"Cannot parse '{rawValue}' as {targetType}")
{
	public string RawValue { get; } = rawValue;

	public string TargetType { get; } = targetType;
}
