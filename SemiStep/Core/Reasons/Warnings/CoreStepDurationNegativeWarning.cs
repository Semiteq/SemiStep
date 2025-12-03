namespace Core.Reasons.Warnings;

public sealed class CoreStepDurationNegativeWarning : BilingualWarning
{
	public int StepIndex { get; }
	public float Duration { get; }

	public CoreStepDurationNegativeWarning(int stepIndex, float duration)
		: base(
			$"Negative step duration ({duration}s) at step {stepIndex}, using 0",
			$"Отрицательная длительность шага ({duration}с) на шаге {stepIndex + 1}, используется 0")
	{
		StepIndex = stepIndex;
		Duration = duration;
		Metadata["stepIndex"] = stepIndex;
		Metadata["duration"] = duration;
	}
}
