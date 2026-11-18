using FluentResults;

using SemiStep.Core.Recipes;

namespace SemiStep.Core.Plc.S7.Protocol;

/// <summary>The PLC recipe header (<c>recipe_lines</c>) disagrees with the lengths of the value arrays.</summary>
public sealed class RecipeLengthMismatchError(
	int headerStepCount,
	RecipeLengthFault fault,
	PropertyType? arrayType = null)
	: Error(BuildMessage(headerStepCount, fault, arrayType))
{
	public int HeaderStepCount { get; } = headerStepCount;

	public RecipeLengthFault Fault { get; } = fault;

	/// <summary>The array that ran out or had leftovers; null for a negative step count.</summary>
	public PropertyType? ArrayType { get; } = arrayType;

	private static string BuildMessage(int headerStepCount, RecipeLengthFault fault, PropertyType? arrayType)
	{
		var arrayName = arrayType?.ToString().ToLowerInvariant();
		return fault switch
		{
			RecipeLengthFault.NegativeStepCount =>
				$"Recipe header declares {headerStepCount} steps, which is negative",
			RecipeLengthFault.ArrayExhausted =>
				$"Recipe header declares {headerStepCount} steps but the {arrayName} array ends early",
			RecipeLengthFault.ArrayHasLeftovers =>
				$"Recipe header declares {headerStepCount} steps but the {arrayName} array holds more values",
			_ => throw new ArgumentOutOfRangeException(nameof(fault), fault, "Unnamed recipe length fault")
		};
	}
}
