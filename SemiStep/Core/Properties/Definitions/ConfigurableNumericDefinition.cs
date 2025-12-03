using System.Globalization;

using Core.Properties.Contracts;
using Core.Reasons.Errors;

namespace Core.Properties.Definitions;

/// <summary>
/// Configurable numeric definition supporting ranges and formatting strategies.
/// Works with int16 and float at runtime (values are stored as float or int16).
/// </summary>
public class ConfigurableNumericDefinition : IPropertyTypeDefinition
{
	private const int Precision = 3;

	private readonly float? _min;
	private readonly float? _max;

	/// <inheritdoc/>
	public FormatKind FormatKind { get; }

	/// <inheritdoc/>
	public object DefaultValue => 0f;

	/// <inheritdoc/>
	public Type SystemType { get; }

	/// <inheritdoc/>
	public string Units { get; }

	/// <inheritdoc/>
	public bool NonNegative { get; }

	/// <inheritdoc/>
	public Result<object> GetNonNegativeValue(object value)
	{
		var numeric = ToFloat(value);

		if (!numeric.HasValue)
			return new CorePropertyNonNumericError();

		if (numeric.Value < 0)
		{
			var absoluteValue = Math.Abs(numeric.Value);
			return SystemType == typeof(short)
				? Result.Ok<object>((short)absoluteValue)
				: Result.Ok<object>(absoluteValue);
		}

		return Result.Ok(value);
	}

	public ConfigurableNumericDefinition(YamlPropertyDefinition dto)
	{
		SystemType = Type.GetType(dto.SystemType, throwOnError: true, ignoreCase: true)!;
		Units = dto.Units;
		NonNegative = dto.NonNegative;
		_min = dto.Min;
		_max = dto.Max;
		FormatKind = Enum.TryParse<FormatKind>(dto.FormatKind, ignoreCase: true, out var parsed)
			? parsed
			: FormatKind.Numeric;
	}

	/// <inheritdoc/>
	public virtual Result TryValidate(object value)
	{
		var numeric = ToFloat(value);

		if (!numeric.HasValue)
			return new CorePropertyValidationFailedError($"expected numeric value, got {value.GetType().Name}");

		if (_min.HasValue && numeric.Value < _min.Value)
			return new CoreNumericValueOutOfRangeError(numeric.Value, _min, _max);

		if (_max.HasValue && numeric.Value > _max.Value)
			return new CoreNumericValueOutOfRangeError(numeric.Value, _min, _max);

		return Result.Ok();
	}

	/// <inheritdoc/>
	public virtual string FormatValue(object value)
	{
		var numeric = ToFloat(value);

		if (!numeric.HasValue)
			return value.ToString() ?? string.Empty;

		return FormatKind switch
		{
			FormatKind.Scientific => numeric.Value.ToString("0.###E0", CultureInfo.InvariantCulture),
			FormatKind.Numeric => numeric.Value.ToString("0.###", CultureInfo.InvariantCulture),
			FormatKind.Int => numeric.Value.ToString("0", CultureInfo.InvariantCulture),
			_ => numeric.Value.ToString(CultureInfo.InvariantCulture)
		};
	}

	/// <inheritdoc/>
	public virtual Result<object> TryParse(string input)
	{
		var sanitized = SanitizeNumericInput(input);

		return SystemType == typeof(short)
			? ParseAsShort(sanitized, input)
			: ParseAsFloat(sanitized, input);
	}

	protected static float? ToFloat(object value) => value switch
	{
		short shortValue => shortValue,
		float floatValue => floatValue,
		_ => null
	};

	private static string SanitizeNumericInput(string input)
	{
		return new string(input.Trim()
				.Where(c => char.IsDigit(c) || c == '.' || c == ',' || c == 'E' || c == 'e' || c == '+' || c == '-' ||
							c == ':')
				.ToArray())
			.Replace(',', '.');
	}

	private static Result<object> ParseAsShort(string sanitized, string originalInput)
	{
		if (short.TryParse(sanitized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var shortValue))
			return Result.Ok<object>(shortValue);

		if (float.TryParse(sanitized, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
			return Result.Ok<object>((short)floatValue);

		return new CorePropertyConversionFailedError(originalInput, "Int16");
	}

	private Result<object> ParseAsFloat(string sanitized, string originalInput)
	{
		if (!float.TryParse(sanitized, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
			return new CorePropertyConversionFailedError(originalInput, "Single");

		if (FormatKind == FormatKind.Int)
			floatValue = (float)Math.Truncate(floatValue);
		else
			floatValue = (float)Math.Round(floatValue, Precision, MidpointRounding.AwayFromZero);

		return Result.Ok<object>(floatValue);
	}
}
