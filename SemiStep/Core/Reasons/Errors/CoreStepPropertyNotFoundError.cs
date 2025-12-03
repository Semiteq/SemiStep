namespace Core.Reasons.Errors;

public sealed class CoreStepPropertyNotFoundError : BilingualError
{
	public string PropertyKey { get; }
	public int RowIndex { get; }

	public CoreStepPropertyNotFoundError(string propertyKey, int rowIndex)
		: base(
			$"Property '{propertyKey}' not found in step at row {rowIndex}",
			$"Свойство '{propertyKey}' не найдено в шаге на строке {rowIndex + 1}")
	{
		PropertyKey = propertyKey;
		RowIndex = rowIndex;
	}
}
