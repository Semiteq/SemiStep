namespace Core.Reasons.Errors;

public sealed class CoreFormulaTargetNotFoundError : BilingualError
{
	public CoreFormulaTargetNotFoundError()
		: base(
			"No target variable found for recalculation",
			"Не найдена целевая переменная для пересчета")
	{
	}
}
