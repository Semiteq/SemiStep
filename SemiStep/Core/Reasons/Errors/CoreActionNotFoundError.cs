namespace Core.Reasons.Errors;

public sealed class CoreActionNotFoundError : BilingualError
{
	public short ActionId { get; }

	public CoreActionNotFoundError(short actionId)
		: base(
			$"Action with ID {actionId} not found",
			$"Действие с ID {actionId} не найдено")
	{
		ActionId = actionId;
	}
}
