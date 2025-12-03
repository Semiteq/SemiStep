namespace Core.Reasons.Errors;

public sealed class CoreStepActionNullError : BilingualError
{
	public int StepIndex { get; }

	public CoreStepActionNullError(int stepIndex)
		: base(
			$"Step at index {stepIndex} has null Action property",
			$"Шаг с индексом {stepIndex + 1} имеет null в свойстве Action")
	{
		StepIndex = stepIndex;
	}
}
