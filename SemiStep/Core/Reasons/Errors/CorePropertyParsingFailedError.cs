namespace Core.Reasons.Errors;

public sealed class CorePropertyParsingFailedError : BilingualError
{
	public string DefaultValue { get; }
	public string ColumnKey { get; }

	public CorePropertyParsingFailedError(string defaultValue, string columnKey)
		: base(
			$"Failed to parse default value '{defaultValue}' for column '{columnKey}'",
			$"Не удалось разобрать значение по умолчанию '{defaultValue}' для столбца '{columnKey}'")
	{
		DefaultValue = defaultValue;
		ColumnKey = columnKey;
	}
}
