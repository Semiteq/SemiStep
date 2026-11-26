using System.Diagnostics;
using System.Globalization;

using Avalonia.Headless.XUnit;

using SemiStep.Core.Plc.State;
using SemiStep.Tests.Performance.Harness;

using Xunit;

namespace SemiStep.Tests.Performance;

// Same-process wall-clock ratio, like TransposedSelectionCostProbe: the cost of one running-line change
// (tracker, bindings, styles and the layout pass it queues) must not grow with the recipe length, because
// both grids virtualise. The line walks inside the first viewport so every change repaints realised cells.
[Trait("Category", "Performance")]
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
public sealed class ExecutionLineChangeCostProbe(ITestOutputHelper output)
{
	private const string ConfigName = "WithGroups";
	private const int WindowWidth = 1200;
	private const int WindowHeight = 800;
	private const int SmallStepCount = 300;
	private const int LargeStepCount = 2100;
	private const int LinesInViewport = 12;
	private const int WarmupChanges = 24;
	private const int ChangesPerRun = 24;
	private const int RunsForMedian = 9;
	private const int DriversPerSize = 5;
	private const double MaxAllowedRatio = 3.0;

	private readonly ITestOutputHelper _output = output;

	[AvaloniaFact(Explicit = true)]
	public Task Canonical_LineChangeCost_StaysFlatAsRecipeGrows()
	{
		return MeasureAndAssertAsync(
			"canonical",
			async stepCount => await CanonicalGridDriver.CreateAsync(ConfigName, stepCount, WindowWidth, WindowHeight));
	}

	[AvaloniaFact(Explicit = true)]
	public Task Transposed_LineChangeCost_StaysFlatAsRecipeGrows()
	{
		return MeasureAndAssertAsync(
			"transposed",
			async stepCount => await TransposedGridDriver.CreateAsync(ConfigName, stepCount, WindowWidth, WindowHeight));
	}

	private async Task MeasureAndAssertAsync(string orientation, Func<int, Task<IRecipeGridDriver>> createDriver)
	{
		// Warm both sizes first so cold JIT does not bias the baseline size.
		await MeasurePerChangeMicrosecondsAsync(createDriver, SmallStepCount);
		await MeasurePerChangeMicrosecondsAsync(createDriver, LargeStepCount);

		// Interleaved fresh drivers: one driver that lands in a slow mode cannot decide the ratio alone.
		var smallSamples = new List<double>(DriversPerSize);
		var largeSamples = new List<double>(DriversPerSize);
		for (var i = 0; i < DriversPerSize; i++)
		{
			smallSamples.Add(await MeasurePerChangeMicrosecondsAsync(createDriver, SmallStepCount));
			largeSamples.Add(await MeasurePerChangeMicrosecondsAsync(createDriver, LargeStepCount));
		}

		var small = Median(smallSamples);
		var large = Median(largeSamples);
		var ratio = small <= 0 ? double.PositiveInfinity : large / small;

		var report = string.Join(
			Environment.NewLine,
			$"{orientation}: {DateTime.Now:yyyy-MM-dd}",
			$"N={SmallStepCount,5}  per-line-change(median) = {small,10:N2} us  drivers: {Format(smallSamples)}",
			$"N={LargeStepCount,5}  per-line-change(median) = {large,10:N2} us  drivers: {Format(largeSamples)}",
			$"ratio N={LargeStepCount} / N={SmallStepCount} = {ratio:F2}x  (limit {MaxAllowedRatio:F1}x)");
		_output.WriteLine(report);
		File.WriteAllText(
			Path.Combine(Path.GetTempPath(), $"semistep_line_change_probe_{orientation}.txt"),
			report);

		Assert.True(
			ratio <= MaxAllowedRatio,
			$"Running-line change cost scaled with recipe size: ratio {ratio:F2}x exceeds {MaxAllowedRatio:F1}x." +
			$"{Environment.NewLine}{report}");
	}

	private static async Task<double> MeasurePerChangeMicrosecondsAsync(
		Func<int, Task<IRecipeGridDriver>> createDriver,
		int stepCount)
	{
		await using var driver = await createDriver(stepCount);

		var line = 0;
		await driver.PushExecutionStateAsync(Running(line));

		for (var i = 0; i < WarmupChanges; i++)
		{
			line = NextLine(line);
			await driver.PushExecutionStateAsync(Running(line));
		}

		GC.Collect();
		GC.WaitForPendingFinalizers();
		await driver.WaitForIdleAsync();

		var samples = new List<double>(RunsForMedian);
		for (var run = 0; run < RunsForMedian; run++)
		{
			var stopwatch = Stopwatch.StartNew();
			for (var i = 0; i < ChangesPerRun; i++)
			{
				line = NextLine(line);
				await driver.PushExecutionStateAsync(Running(line));
			}

			stopwatch.Stop();
			samples.Add(stopwatch.Elapsed.TotalMilliseconds * 1000.0 / ChangesPerRun);
		}

		return Median(samples);
	}

	private static int NextLine(int line)
	{
		return (line + 1) % LinesInViewport;
	}

	private static PlcExecutionInfo Running(int line)
	{
		return PlcExecutionInfo.Empty with { RecipeActive = true, ActualLine = line };
	}

	private static string Format(List<double> samples)
	{
		return string.Join(" ", samples.Select(sample => sample.ToString("F0", CultureInfo.InvariantCulture)));
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
