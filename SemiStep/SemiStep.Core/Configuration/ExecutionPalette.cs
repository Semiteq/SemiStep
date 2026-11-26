namespace SemiStep.Core.Configuration;

/// <summary>The execution green and its blend weights (0..1) for the running row and the past rows.</summary>
public sealed record ExecutionPalette(
	StyleColor Running,
	double RunningWeight,
	double PastWeight);
