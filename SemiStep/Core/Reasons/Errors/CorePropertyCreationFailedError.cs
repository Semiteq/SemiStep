namespace Core.Reasons.Errors;

public sealed class CorePropertyCreationFailedError : BilingualError
{
	public string ColumnKey { get; }

	public CorePropertyCreationFailedError(string columnKey)
		: base(
			$"Failed to create property for column '{columnKey}'",
			$"Не удалось создать свойство для столбца '{columnKey}'")
	{
		ColumnKey = columnKey;
	}
}
