namespace Core.Reasons.Errors;

public sealed class CoreFormulaNotFoundError : BilingualError
{
	public short ActionId { get; }

	public CoreFormulaNotFoundError(short actionId)
		: base(
			$"No compiled formula found for action ID {actionId}",
			$"Не найдена скомпилированная формула для действия с ID {actionId}")
	{
		ActionId = actionId;
	}
}
