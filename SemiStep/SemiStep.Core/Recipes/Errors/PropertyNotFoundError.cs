using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class PropertyNotFoundError(string propertyTypeId)
	: Error($"Property '{propertyTypeId}' not found")
{
	public string PropertyTypeId { get; } = propertyTypeId;
}
