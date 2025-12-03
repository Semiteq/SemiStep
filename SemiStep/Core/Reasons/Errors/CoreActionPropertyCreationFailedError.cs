namespace Core.Reasons.Errors;

public sealed class CoreActionPropertyCreationFailedError : BilingualError
{
	public short ActionId { get; }

	public CoreActionPropertyCreationFailedError(short actionId)
		: base(
			$"Failed to create action property for action ID {actionId}",
			$"Не удалось создать свойство действия для действия с ID {actionId}")
	{
		ActionId = actionId;
	}
}
