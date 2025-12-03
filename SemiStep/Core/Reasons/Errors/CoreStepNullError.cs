namespace Core.Reasons.Errors;

public sealed class CoreStepNullError : BilingualError
{
	public int StepIndex { get; }

	public CoreStepNullError(int stepIndex)
		: base(
			$"Step at index {stepIndex} is null",
			$"Шаг с индексом {stepIndex + 1} равен null")
	{
		StepIndex = stepIndex;
	}
}
