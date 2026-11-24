using FluentResults;

namespace SemiStep.Core.Configuration.Validation;

internal sealed class StringMaxLengthMissingError(string propertyTypeId)
	: Error($"[properties, PropertyTypeId='{propertyTypeId}'] String property max_length is required")
{
	public string PropertyTypeId { get; } = propertyTypeId;
}
