using FluentResults;

namespace SemiStep.Core.Configuration.Validation;

internal sealed class StringMaxLengthNotPositiveError(string propertyTypeId, int maxLength)
	: Error(
		$"[properties, PropertyTypeId='{propertyTypeId}'] String property max_length must be positive, "
		+ $"got: {maxLength}")
{
	public string PropertyTypeId { get; } = propertyTypeId;

	public int MaxLength { get; } = maxLength;
}
