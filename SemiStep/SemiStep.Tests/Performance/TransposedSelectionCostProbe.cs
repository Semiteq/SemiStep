using System.Diagnostics;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.Performance;

// Selection-cost regression guard for TransposedRecipeGridView.OnSelectionChanged.
//
// UNLIKE the report-only TransposedViewAllocationProbe, this probe ASSERTS a ratio (deliberate
// departure): it is the checked-in instrument that catches a re-introduction of the O(S*N) IndexOf
// scan the fix removed. It is still env-gated out of CI (SEMISTEP_PROBE=1), because the measurement
// is wall-clock and seeding 2100 steps is slow.
//
// Design that isolates the regression:
//   - The selection size S is held CONSTANT (a fixed tail range) while N grows across 300 / 1200 /
//     4800. Select-all would make S = N and force O(N) even with the fix, so it cannot tell a flat
//     handler from a linear one. A fixed-S tail range is what exposes IndexOf-in-N: the old handler
//     summed IndexOf over the selected items, each ~N deep, so its per-event cost grew linearly in N;
//     the fix reads Selection.SelectedIndexes (O(S)) and stays flat.
//   - The driven operation is a toggle of one selected tail column off then on, issued through the
//     INDEX-based selection model (Selection.Deselect(index) / Selection.Select(index)). Deselect
//     then re-select of a tail index raises SelectionChanged (RemovedItems / AddedItems), so it
//     routes through OnSelectionChanged synchronously. The index-based API is used ON PURPOSE instead
//     of SelectedItems.Remove/Add: the latter resolve item->index via an O(N) IndexOf over the source
//     collection INSIDE the timed window, injecting an N-growing harness cost that both adds noise and
//     mimics the very regression under test. Index-shifting inserts are deliberately NOT used: they
//     raise IndexesChanged, which SelectingItemsControl does not surface as SelectionChanged.
//   - CRITICAL: only the selection mutations are inside the stopwatch. The Dispatcher.RunJobs()
//     re-render (layout + cell realization) is kept OUT of the timed region. That render floor is a
//     large, N-independent, GC-noisy cost that swamped the handler in the first cut of this probe
//     (its fixed-S baseline was non-monotonic: 191 / 314 / 125 us, proving the handler sat below the
//     floor). With the render excluded, what remains in the timed window is the selection-model
//     diff plus OnSelectionChanged itself, so the O(S) fix and the O(S*N) regression separate cleanly.
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

	[AvaloniaFact]
	public async Task SelectionChangedCost_StaysFlatAsRecipeGrows()
	{
		Assert.SkipUnless(
			Environment.GetEnvironmentVariable("SEMISTEP_PROBE") == "1",
			"Measurement probe: set SEMISTEP_PROBE=1 to run.");

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
		var fixture = new UIFixture();
		await fixture.InitializeAsync();
		try
		{
			fixture.SeedRecipe(stepCount);

			var surface = fixture.CreateTransposedSurface();
			surface.Initialize();
			var view = new TransposedRecipeGridView { DataContext = surface };
			var window = new Window { Width = 1200, Height = 800, Content = view };
			window.Show();
			Dispatcher.UIThread.RunJobs();

			var stepListBox = view.FindControl<ListBox>("StepListBox")!;
			var selection = stepListBox.Selection;

			// Fixed tail range [N-200 .. N-1], selected through the index-based model (no item->index
			// lookup): positioned so the old IndexOf scan was ~N deep per selected item.
			var rangeStart = stepCount - SelectionSize;
			for (var index = rangeStart; index < stepCount; index++)
			{
				selection.Select(index);
			}

			Dispatcher.UIThread.RunJobs();

			var toggledIndex = stepCount - 1;

			for (var i = 0; i < WarmupToggles; i++)
			{
				selection.Deselect(toggledIndex);
				selection.Select(toggledIndex);
			}

			Dispatcher.UIThread.RunJobs();

			var perOpSamples = new List<double>(RunsForMedian);
			for (var run = 0; run < RunsForMedian; run++)
			{
				var stopwatch = Stopwatch.StartNew();
				for (var i = 0; i < TogglesPerRun; i++)
				{
					// Deselect then re-select raises SelectionChanged synchronously twice, so the handler
					// runs inside this timed window. Driving the index-based model keeps the O(N) item->index
					// lookup that SelectedItems.Remove/Add would inject OUT of the measurement. RunJobs (the
					// re-render floor) stays OUT of the timed window too, on purpose.
					selection.Deselect(toggledIndex);
					selection.Select(toggledIndex);
				}

				stopwatch.Stop();

				// Drain the deferred re-render outside the measured window so it never counts toward the
				// handler cost and the dispatcher queue does not grow across runs.
				Dispatcher.UIThread.RunJobs();

				// Two SelectionChanged events per toggle (deselect, then select).
				var events = TogglesPerRun * 2;
				perOpSamples.Add(stopwatch.Elapsed.TotalMilliseconds * 1000.0 / events);
			}

			window.Close();

			return Median(perOpSamples);
		}
		finally
		{
			await fixture.DisposeAsync();
		}
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
