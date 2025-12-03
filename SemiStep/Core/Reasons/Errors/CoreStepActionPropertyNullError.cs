namespace Core.Reasons.Errors;

public sealed class CoreStepActionPropertyNullError : BilingualError
{
	public int RowIndex { get; }

	public CoreStepActionPropertyNullError(int rowIndex)
		: base(
			$"Action property is null at row {rowIndex}",
			$"Свойство действия равно null на строке {rowIndex + 1}")
	{
		RowIndex = rowIndex;
	}
}
