namespace Core.Properties;

/// <summary>
/// Defines supported formatting strategies for numeric property values.
/// </summary>
public enum FormatKind
{
	/// <summary>
	/// Standard numeric format (e.g., "123.456").
	/// </summary>
	Numeric = 0,

	/// <summary>
	/// Scientific notation format (e.g., "1.234E+02").
	/// </summary>
	Scientific = 1,

	/// <summary>
	/// Time format in hours:minutes:seconds (e.g., "01:23:45").
	/// </summary>
	TimeHms = 2,

	/// <summary>
	/// Displayed as integer value (e.g., "123").
	/// </summary>
	Int = 3
}
