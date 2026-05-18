using System.Globalization;

using FluentResults;

namespace SemiStep.Core.Recipes.Formulas.Errors;

public sealed class FormulaTargetOutOfRangeError : Error
{
	public FormulaTargetOutOfRangeError(string target, double value, double? min, double? max)
		: base(BuildMessage(target, value, min, max))
	{
		Target = target;
		Value = value;
		Min = min;
		Max = max;
	}

	public string Target { get; }

	public double Value { get; }

	public double? Min { get; }

	public double? Max { get; }

	private static string BuildMessage(string target, double value, double? min, double? max)
	{
		var culture = CultureInfo.InvariantCulture;
		var bounds = (min, max) switch
		{
			(double minimum, double maximum) => $"[{minimum.ToString(culture)}; {maximum.ToString(culture)}]",
			(double minimum, null) => $"[{minimum.ToString(culture)}; +∞)",
			(null, double maximum) => $"(-∞; {maximum.ToString(culture)}]",
			_ => "(unbounded)"
		};

		return $"Recalculated value {value.ToString(culture)} for target '{target}' is outside allowed range {bounds}.";
	}
}
