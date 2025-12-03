namespace Core.Reasons.Warnings;

public sealed class CoreForLoopInvalidIterationCountWarning : BilingualWarning
{
	public int StepIndex { get; }
	public int IterationCount { get; }

	public CoreForLoopInvalidIterationCountWarning(int stepIndex, int iterationCount)
		: base(
			$"Invalid iteration count ({iterationCount}) at step {stepIndex}",
			$"Недопустимое количество итераций ({iterationCount}) на шаге {stepIndex + 1}")
	{
		StepIndex = stepIndex;
		IterationCount = iterationCount;
		Metadata["stepIndex"] = stepIndex;
		Metadata["iterations"] = iterationCount;
	}
}
