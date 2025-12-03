using Core.Properties.Contracts;
using Core.Reasons.Errors;

namespace Core.Properties.Definitions;

/// <summary>
/// Configurable string definition with optional MaxLength constraint.
/// </summary>
public sealed class ConfigurableStringDefinition : IPropertyTypeDefinition
{
	private const int DefaultMaxLength = 255;
	private readonly int _maxLength;

	/// <inheritdoc/>
	public string Units => string.Empty;

	/// <inheritdoc/>
	public bool NonNegative => false;

	/// <inheritdoc/>
	public Result<object> GetNonNegativeValue(object value) => value;

	/// <inheritdoc/>
	public Type SystemType => typeof(string);

	/// <inheritdoc/>
	public FormatKind FormatKind => FormatKind.Numeric;

	/// <inheritdoc/>
	public object DefaultValue => string.Empty;

	public ConfigurableStringDefinition(YamlPropertyDefinition dto)
	{
		_maxLength = Math.Max(0, dto.MaxLength ?? DefaultMaxLength);
	}

	/// <inheritdoc/>
	public Result TryValidate(object value)
	{
		var s = value?.ToString() ?? string.Empty;
		return s.Length > _maxLength
			? new CoreStringLengthExceededError(s.Length, _maxLength)
			: Result.Ok();
	}

	/// <inheritdoc/>
	public string FormatValue(object value) => value?.ToString() ?? string.Empty;

	/// <inheritdoc/>
	public Result<object> TryParse(string input) => Result.Ok<object>(input ?? string.Empty);
}
