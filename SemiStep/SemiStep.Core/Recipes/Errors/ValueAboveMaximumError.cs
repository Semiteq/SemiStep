using System;

using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class ValueAboveMaximumError(double value, double max, string id)
	: Error(FormattableString.Invariant($"Value {value} exceeds maximum {max} for '{id}'"))
{
	public double Value { get; } = value;

	public double Max { get; } = max;

	public string Id { get; } = id;
}
