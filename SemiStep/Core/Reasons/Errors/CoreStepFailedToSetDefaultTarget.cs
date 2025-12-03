namespace Core.Reasons.Errors;

public sealed class CoreStepFailedToSetDefaultTarget : BilingualError
{
	public CoreStepFailedToSetDefaultTarget(string columnKey)
		: base($"Failed to set default target for column {columnKey}",
			$"Не удалось установить значение цели по умолчанию для столбца {columnKey}"
		)
	{
	}
}
