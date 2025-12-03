using Core.Loops;
using Core.Snapshot;

using Microsoft.Extensions.Logging;

namespace Core.Runtime;

/// <summary>
/// Timer logic consuming analysis snapshot only. Loop offsets ignored when loop integrity compromised.
/// </summary>
public sealed class TimerService : ITimerService
{
	private readonly ILogger<TimerService> _logger;
	private TimeSpan _lastTotalElapsed = TimeSpan.Zero;
	private int _lastStepIndex = -1;
	private bool _staticMode = true;

	public event Action<TimeSpan, TimeSpan>? TimesUpdated;

	public TimerService(ILogger<TimerService> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public void Reset()
	{
		_staticMode = true;
		_lastTotalElapsed = TimeSpan.Zero;
		_lastStepIndex = -1;
	}

	public void UpdateRuntime(RecipeRuntimeSnapshot runtimeSnapshot, RecipeAnalysisSnapshot analysisSnapshot)
	{
		var startTimes = analysisSnapshot.StepStartTimes;
		var total = analysisSnapshot.TotalDuration;

		if (!runtimeSnapshot.RecipeActive)
		{
			_staticMode = true;
			_lastTotalElapsed = TimeSpan.Zero;
			_lastStepIndex = -1;

			if (startTimes.Count == 0)
			{
				TimesUpdated?.Invoke(TimeSpan.Zero, TimeSpan.Zero);
				return;
			}

			TimesUpdated?.Invoke(TimeSpan.Zero, total);
			return;
		}

		if (_staticMode)
		{
			_staticMode = false;
			_lastTotalElapsed = TimeSpan.Zero;
			_lastStepIndex = -1;
		}

		if (startTimes.Count == 0)
		{
			TimesUpdated?.Invoke(TimeSpan.Zero, TimeSpan.Zero);
			return;
		}

		if (runtimeSnapshot.StepIndex < 0
			|| runtimeSnapshot.StepIndex >= analysisSnapshot.StepCount
			|| !startTimes.TryGetValue(runtimeSnapshot.StepIndex, out var baseStart))
		{
			TimesUpdated?.Invoke(TimeSpan.Zero, TimeSpan.Zero);
			return;
		}

		var elapsedInStep = TimeSpan.FromSeconds(runtimeSnapshot.StepElapsedSeconds);

		var loopOffset = TimeSpan.Zero;
		if (!analysisSnapshot.Flags.LoopIntegrityCompromised)
		{
			loopOffset = CalculateLoopOffset(runtimeSnapshot, analysisSnapshot);
		}

		var totalElapsed = baseStart + loopOffset + elapsedInStep;

		if (runtimeSnapshot.StepIndex == _lastStepIndex && totalElapsed < _lastTotalElapsed)
		{
			// Clamp to the last known total elapsed time in case of regressions
			totalElapsed = _lastTotalElapsed;
		}

		_lastStepIndex = runtimeSnapshot.StepIndex;
		_lastTotalElapsed = totalElapsed;

		var totalLeft = total > totalElapsed ? total - totalElapsed : TimeSpan.Zero;

		var nextIndex = runtimeSnapshot.StepIndex + 1;
		var nextStart = startTimes.TryGetValue(nextIndex, out var ns) ? ns : total;
		var stepDuration = nextStart - baseStart;
		var stepLeft = stepDuration > elapsedInStep ? stepDuration - elapsedInStep : TimeSpan.Zero;

		TimesUpdated?.Invoke(stepLeft, totalLeft);
	}

	private TimeSpan CalculateLoopOffset(RecipeRuntimeSnapshot snapshot, RecipeAnalysisSnapshot analysis)
	{
		var loops = analysis.LoopTree.EnclosingLoopsForStep.TryGetValue(snapshot.StepIndex, out var list)
			? list
			: Array.Empty<LoopNode>();

		var offset = TimeSpan.Zero;
		foreach (var loop in loops)
		{
			if (loop.SingleIterationDuration is null || loop.EffectiveIterationCount <= 0)
				continue;

			var completedIterations = Math.Max(0, GetCompletedIterations(snapshot, loop.NestingDepth));

			if (completedIterations >= loop.EffectiveIterationCount)
				completedIterations = loop.EffectiveIterationCount - 1;

			offset += TimeSpan.FromTicks(loop.SingleIterationDuration.Value.Ticks * completedIterations);
		}

		return offset;
	}

	private static int GetCompletedIterations(RecipeRuntimeSnapshot snapshot, int nestingDepth) =>
		nestingDepth switch
		{
			1 => snapshot.ForLevel1Count,
			2 => snapshot.ForLevel2Count,
			3 => snapshot.ForLevel3Count,
			_ => 0
		};
}
