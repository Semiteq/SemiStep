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
		Metadata["target"] = target;
		Metadata["value"] = value;
		if (min.HasValue)
		{
			Metadata["min"] = min.Value;
		}

		if (max.HasValue)
		{
			Metadata["max"] = max.Value;
		}
	}

	public string Target { get; }

	public double Value { get; }

	public double? Min { get; }

	public double? Max { get; }

	private static string BuildMessage(string target, double value, double? min, double? max)
	{
		var bounds = (min, max) switch
		{
			(double minimum, double maximum) => $"[{minimum}; {maximum}]",
			(double minimum, null) => $"[{minimum}; +∞)",
			(null, double maximum) => $"(-∞; {maximum}]",
			_ => "(unbounded)"
		};

		return $"Recalculated value {value} for target '{target}' is outside allowed range {bounds}.";
	}
}
