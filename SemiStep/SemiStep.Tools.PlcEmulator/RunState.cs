namespace SemiStep.Tools.PlcEmulator;

/// <summary>
/// <see cref="ActualLine"/> is the raw DB996 value: -1 for the first tick, the 0-based row, N after the end.
/// </summary>
internal readonly record struct RunState(RunPhase Phase, int ActualLine, int LineCount)
{
	public bool IsActive => Phase != RunPhase.Idle;

	/// <summary>Clamped because the natural end stores N and a stop before the first line stores -1.</summary>
	public int DisplayLine => Math.Min(Math.Max(ActualLine + 1, 1), LineCount);

	public string Text => Phase switch
	{
		RunPhase.Idle => "Idle",
		RunPhase.Starting => "Running: starting",
		RunPhase.Running => $"Running: line {DisplayLine} of {LineCount}",
		RunPhase.Paused => $"Paused at line {DisplayLine}: press Continue",
		RunPhase.Finishing => $"Finishing: line {DisplayLine} of {LineCount}",
		_ => throw new InvalidOperationException($"Unnamed run phase {Phase}.")
	};
}
