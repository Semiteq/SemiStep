namespace Core.Reasons.Warnings;

public sealed class CoreForLoopUnmatchedWarning : BilingualWarning
{
	public int StepIndex { get; }
	public string Details { get; }

	public int? StartIndex { get; }
	public int? EndIndex { get; }

	public CoreForLoopUnmatchedWarning(int stepIndex, string details = "")
		: base(
			string.IsNullOrEmpty(details)
				? $"Unmatched ForLoop at step {stepIndex}"
				: $"Unmatched ForLoop at step {stepIndex}: {details}",
			string.IsNullOrEmpty(details)
				? $"Незакрытый ForLoop на шаге {stepIndex + 1}"
				: $"Незакрытый ForLoop на шаге {stepIndex + 1}: {details}")
	{
		StepIndex = stepIndex;
		Details = details;
		Metadata["stepIndex"] = stepIndex;
		if (!string.IsNullOrEmpty(details))
			Metadata["details"] = details;
	}

	public CoreForLoopUnmatchedWarning(int stepIndex, int? startIndex, int? endIndex, string details = "")
		: base(
			string.IsNullOrEmpty(details)
				? $"Unmatched ForLoop at step {stepIndex}"
				: $"Unmatched ForLoop at step {stepIndex}: {details}",
			string.IsNullOrEmpty(details)
				? $"Незакрытый ForLoop на шаге {stepIndex + 1}"
				: $"Незакрытый ForLoop на шаге {stepIndex + 1}: {details}")
	{
		StepIndex = stepIndex;
		Details = details;
		StartIndex = startIndex;
		EndIndex = endIndex;

		Metadata["stepIndex"] = stepIndex;
		if (startIndex.HasValue)
			Metadata["startIndex"] = startIndex.Value;
		if (endIndex.HasValue)
			Metadata["endIndex"] = endIndex.Value;
		if (!string.IsNullOrEmpty(details))
			Metadata["details"] = details;
	}
}
