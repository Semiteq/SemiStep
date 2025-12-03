using System.Globalization;

using Core.Properties.Contracts;
using Core.Reasons.Errors;

namespace Core.Properties.Definitions;

public sealed class ConfigurableEnumDefinition : IPropertyTypeDefinition
{
	/// <inheritdoc/>
	public string Units { get; }

	/// <inheritdoc/>
	public Type SystemType => typeof(short);

	/// <inheritdoc/>
	public FormatKind FormatKind => FormatKind.Numeric;

	/// <inheritdoc/>
	public object DefaultValue => (short)0;

	/// <inheritdoc/>
	public bool NonNegative => false;

	/// <inheritdoc/>
	public Result<object> GetNonNegativeValue(object value) => value;

	public ConfigurableEnumDefinition(YamlPropertyDefinition dto)
	{
		Units = dto.Units;
	}

	/// <inheritdoc/>
	public Result TryValidate(object value)
		=> value is short
			? Result.Ok()
			: new CorePropertyValidationFailedError("value must be Int16");

	/// <inheritdoc/>
	public string FormatValue(object value) => value.ToString();

	/// <inheritdoc/>
	public Result<object> TryParse(string input)
	{
		const NumberStyles NumberStyles = NumberStyles.Integer;
		var invariantCulture = CultureInfo.InvariantCulture;

		return short.TryParse(input, NumberStyles, invariantCulture, out var s)
			? Result.Ok<object>(s)
			: new CorePropertyConversionFailedError(input, "Int16");
	}
}
