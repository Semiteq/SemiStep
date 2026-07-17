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

// Selection-cost regression guard for TransposedRecipeGridView.OnSelectionChanged, migrated onto the
// black-box harness. The view is built and the fixed tail range is selected through the driver
// (TransposedGridDriver.CreateAsync + SelectRangeAsync); the driver "supplies the selection actions".
//
// This is the one CPU-bound, allocation-neutral gate: PerfSignals (allocated bytes / fresh visuals)
// CANNOT express it, so the measurement is NOT routed through PerfScenarioRunner. It stays a same-process
// Stopwatch wall-clock RATIO local to this probe (dividing two timings from one process cancels machine
// speed), which is the only signal that discriminates the O(S*N) IndexOf scan the fix removed.
//
// Explicit measurement fact: plain `dotnet test`/CI does not run it (xunit v3 Explicit). Run:
//   SemiStep/Artifacts/bin/SemiStep.Tests/<config>/SemiStep.Tests.exe \
//     -explicit only -method "*SelectionCostProbe*"
//
// Design that isolates the regression:
//   - The selection size S is held CONSTANT (a fixed tail range) while N grows across 300 / 1200 /
//     4800. Select-all would make S = N and force O(N) even with the fix, so it cannot tell a flat
//     handler from a linear one. A fixed-S tail range is what exposes IndexOf-in-N: the old handler
//     summed IndexOf over the selected items, each ~N deep, so its per-event cost grew linearly in N;
//     the fix reads Selection.SelectedIndexes (O(S)) and stays flat.
//   - The driven operation is a toggle of one selected tail column off then on, issued through the
//     INDEX-based selection model (driver.Selection.Deselect(index) / Select(index)). Deselect then
//     re-select of a tail index raises SelectionChanged (RemovedItems / AddedItems), so it routes
//     through OnSelectionChanged synchronously. The index-based API is used ON PURPOSE instead of
//     SelectedItems.Remove/Add: the latter resolve item->index via an O(N) IndexOf over the source
//     collection INSIDE the timed window, injecting an N-growing harness cost that both adds noise and
//     mimics the very regression under test. Index-shifting inserts are deliberately NOT used: they
//     raise IndexesChanged, which SelectingItemsControl does not surface as SelectionChanged.
//   - CRITICAL: only the selection mutations are inside the stopwatch. The dispatcher pump (layout +
//     cell realization) is kept OUT of the timed region (driver.WaitForIdleAsync() runs after Stop).
//     That render floor is a large, N-independent, GC-noisy cost that swamped the handler in the first
//     cut of this probe. With the render excluded, what remains in the timed window is the
//     selection-model diff plus OnSelectionChanged itself, so the O(S) fix and the O(S*N) regression
//     separate cleanly.
//   - S is 200 (not 100) so the regression's S*N comparison count is unmistakable at large N, and
//     the per-event cost stays measurably above stopwatch noise.
//   - Per-event cost is a MEDIAN of repeated runs to absorb GC/JIT jitter.
//   - Before ANY measurement is recorded, the full measured path is exercised once for EVERY N
//     (a discarded warmup pass over the whole step-count set). Median-of-runs cancels within-fixture
//     jitter but not a fixture-wide cold-JIT/GC skew, and the baseline N is measured first in a fresh
//     fixture; the warmup pass puts the baseline and the largest-N fixture on equal JIT footing so the
//     ratio's denominator is not inflated by first-run warmup.
//
// Assertion: per-op cost at N=4800 <= 3x per-op cost at N=300. The fix stays near 1x (flat in N once
// the render floor is removed); reintroducing the StepColumns.IndexOf scan makes it grow ~16x (linear
// in N at fixed S), far past the 3x guard. Discrimination was verified by temporarily restoring the
// IndexOf scan and confirming this probe FAILS; see the plan for the recorded fix/regression numbers.
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
			await MeasurePerEventCostAsync(stepCount);
		}

		foreach (var stepCount in _stepCounts)
		{
			var perOpMicroseconds = await MeasurePerEventCostAsync(stepCount);
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

	// Returns the median (across RunsForMedian) per-event wall-clock cost in microseconds of one
	// OnSelectionChanged invocation, with a fixed 200-column tail selection over a recipe of stepCount.
	private static async Task<double> MeasurePerEventCostAsync(int stepCount)
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
