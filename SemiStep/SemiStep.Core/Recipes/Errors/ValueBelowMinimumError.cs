using System;

using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class ValueBelowMinimumError(double value, double min, string id)
	: Error(FormattableString.Invariant($"Value {value} is below minimum {min} for '{id}'"))
{
	public double Value { get; } = value;

	public double Min { get; } = min;

	public string Id { get; } = id;
}
