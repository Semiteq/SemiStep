namespace Core.Reasons.Errors;

public sealed class CoreStepMissingActionError : BilingualError
{
	public int StepIndex { get; }

	public CoreStepMissingActionError(int stepIndex)
		: base(
			$"Step at index {stepIndex} is missing Action property",
			$"Шаг с индексом {stepIndex + 1} не содержит свойство Action")
	{
		StepIndex = stepIndex;
	}
}
