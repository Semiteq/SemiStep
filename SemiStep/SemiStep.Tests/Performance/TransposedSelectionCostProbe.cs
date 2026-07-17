using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Headless.XUnit;

using SemiStep.Tests.Performance.Harness;

using Xunit;

namespace SemiStep.Tests.Performance;

// CPU-bound, allocation-neutral gate: PerfSignals (bytes / fresh visuals) cannot express it, so the
// measurement is NOT routed through PerfScenarioRunner. It stays a same-process Stopwatch wall-clock
// RATIO local to this probe (dividing two timings from one process cancels machine speed), the only
// signal that discriminates the O(S*N) IndexOf scan the fix removed.
//
// S is held CONSTANT (a fixed tail range) while N grows: select-all would make S = N and hide a linear
// handler; S=200 keeps the regression's S*N cost above stopwatch noise. Index-shifting inserts are not
// used: they raise IndexesChanged, which SelectingItemsControl does not surface as SelectionChanged.
[Trait("Category", "Performance")]
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
public sealed class TransposedSelectionCostProbe
{
	private const string ConfigName = "WithGroups";
	private const int WindowWidth = 1200;
	private const int WindowHeight = 800;
	private const int SelectionSize = 200;
	private const int WarmupToggles = 8;
	private const int TogglesPerRun = 40;
	private const int RunsForMedian = 9;
	private const double MaxAllowedRatio = 3.0;

	private static readonly int[] _stepCounts = { 300, 1200, 4800 };

	private readonly ITestOutputHelper _output;

	public TransposedSelectionCostProbe(ITestOutputHelper output)
	{
		_output = output;
	}

	[AvaloniaFact(Explicit = true)]
	public async Task SelectionChangedCost_StaysFlatAsRecipeGrows()
	{
		var lines = new List<string>();
		var perOpByStepCount = new Dictionary<int, double>();

		// Warmup: run the full measured path once for every N (including the baseline N) before any
		// measurement is recorded, so cold-JIT/GC does not bias the first (baseline) fixture and every
		// N is on equal footing. Results are discarded.
		foreach (var stepCount in _stepCounts)
		{
			await MeasurePerEventMicrosecondsAsync(stepCount);
		}

		foreach (var stepCount in _stepCounts)
		{
			var perOpMicroseconds = await MeasurePerEventMicrosecondsAsync(stepCount);
			perOpByStepCount[stepCount] = perOpMicroseconds;
			lines.Add(
				$"N={stepCount,5}  S={SelectionSize,4}  per-selection-event(median) = {perOpMicroseconds,10:N2} us");
		}

		var baseline = perOpByStepCount[_stepCounts[0]];
		var largest = perOpByStepCount[_stepCounts[^1]];
		var ratio = baseline <= 0 ? double.PositiveInfinity : largest / baseline;

		lines.Add(string.Empty);
		lines.Add(
			$"ratio N={_stepCounts[^1]} / N={_stepCounts[0]} = {ratio:F2}x  " +
			$"(limit {MaxAllowedRatio:F1}x; a linear IndexOf regression would show ~{_stepCounts[^1] / _stepCounts[0]}x)");

		var report = string.Join(Environment.NewLine, lines);
		_output.WriteLine(report);
		File.WriteAllText(Path.Combine(Path.GetTempPath(), "semistep_selection_probe.txt"), report);

		Assert.True(
			ratio <= MaxAllowedRatio,
			$"Per-selection-event cost scaled with recipe size: ratio {ratio:F2}x exceeds {MaxAllowedRatio:F1}x. " +
			$"This is the O(S*N) IndexOf regression the fix removed. Report:{Environment.NewLine}{report}");
	}

	private static async Task<double> MeasurePerEventMicrosecondsAsync(int stepCount)
	{
		await using var driver = await TransposedGridDriver.CreateAsync(
			ConfigName, stepCount, WindowWidth, WindowHeight);

		// Fixed tail range [N-200 .. N-1], established through the driver: positioned so the old IndexOf
		// scan was ~N deep per selected item. This setup is outside every timed window.
		var rangeStart = stepCount - SelectionSize;
		await driver.SelectRangeAsync(rangeStart, stepCount - 1);

		var selection = driver.Selection;
		var toggledIndex = stepCount - 1;

		for (var i = 0; i < WarmupToggles; i++)
		{
			selection.Deselect(toggledIndex);
			selection.Select(toggledIndex);
		}

		await driver.WaitForIdleAsync();

		var perOpSamples = new List<double>(RunsForMedian);
		for (var run = 0; run < RunsForMedian; run++)
		{
			var stopwatch = Stopwatch.StartNew();
			for (var i = 0; i < TogglesPerRun; i++)
			{
				// Deselect then re-select raises SelectionChanged synchronously twice, so the handler
				// runs inside this timed window. Driving the index-based model keeps the O(N) item->index
				// lookup that SelectedItems.Remove/Add would inject OUT of the measurement. The dispatcher
				// pump (the re-render floor) stays OUT of the timed window too, on purpose.
				selection.Deselect(toggledIndex);
				selection.Select(toggledIndex);
			}

			stopwatch.Stop();

			// Drain the deferred re-render outside the measured window so it never counts toward the
			// handler cost and the dispatcher queue does not grow across runs.
			await driver.WaitForIdleAsync();

			// Two SelectionChanged events per toggle (deselect, then select).
			var events = TogglesPerRun * 2;
			perOpSamples.Add(stopwatch.Elapsed.TotalMilliseconds * 1000.0 / events);
		}

		return Median(perOpSamples);
	}

	private static double Median(List<double> samples)
	{
		var sorted = samples.OrderBy(value => value).ToList();
		var middle = sorted.Count / 2;

		return sorted.Count % 2 == 1
			? sorted[middle]
			: (sorted[middle - 1] + sorted[middle]) / 2.0;
	}
}
