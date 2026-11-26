using System.Globalization;

using FluentResults;

namespace SemiStep.Core.Configuration;

public sealed class GridStyleWeightOutOfRangeError(string section, string key, double value)
	: Error(string.Create(
		CultureInfo.InvariantCulture,
		$"Grid style '{section}.{key}' has weight {value}; expected a value from 0 to 1."))
{
	public string Section { get; } = section;

	public string Key { get; } = key;

	public double Value { get; } = value;
}
