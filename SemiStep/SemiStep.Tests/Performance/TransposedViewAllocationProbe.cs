using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Tests.Performance.Harness;

using Xunit;

namespace SemiStep.Tests.Performance;

// Black-box scroll/recycle gates over framework-boundary signals. Explicit tests: commands and the gate
// hierarchy live in Docs/perf/README.md.
[Trait("Category", "Performance")]
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
public sealed class TransposedViewAllocationProbe
{
	// Scroll gate: enough columns to virtualize in a 1400px window, scrolled ~200 apart per round-trip.
	private const string ScrollConfig = "WideParams";
	private const int ScrollWindowWidth = 1400;
	private const int ScrollWindowHeight = 800;
	private const int ScrollColumns = 420;
	private const int ScrollLowColumn = 20;
	private const int ScrollHighColumn = 220;
	private const int ScrollRoundTrips = 20;

	// Viewport-jump: a single far jump (start -> end) after a warmup round-trip, measured on both drivers so
	// the transposed/canonical parity ratio comes from one scenario body.
	private const string JumpConfig = "WideParams";
	private const int JumpSeed = 120;
	private const int TransposedWindowWidth = 1400;
	private const int TransposedWindowHeight = 800;
	private const int CanonicalWindowWidth = 1400;
	private const int CanonicalWindowHeight = 800;

	// The parity ratio rides a ~10% margin, so each side is measured as a median of a few jumps to absorb
	// GC/JIT jitter rather than trusting a single shot.
	private const int ParitySampleCount = 3;

	// ~10% over the measured ~3.0x worst case while TransposedColumnCellsHost + pool remain; tightens to
	// 2.0x after their deletion (see Docs/perf/README.md, gate hierarchy).
	private const double ParityRatioLimit = 3.3;
	private const double ParityRatioTargetAfterHostPoolDeletion = 2.0;

	private const string ScalingConfig = "WideParams";
	private const int ScalingWindowWidth = 1400;
	private const int ScalingWindowHeight = 800;
	private const int ScalingSmallSeed = 20;
	private const int ScalingLargeSeed = 120;
	private const int WarmupAppends = 6;
	private const int MeasuredAppends = 12;
	private const double ScalingRatioLimit = 1.5;

	private readonly ITestOutputHelper _output;
	private readonly PerfActualsFixture _actuals;

	public TransposedViewAllocationProbe(ITestOutputHelper output, PerfActualsFixture actuals)
	{
		_output = output;
		_actuals = actuals;
	}

	[AvaloniaFact(Explicit = true)]
	public async Task ScrollRoundTrips_CreateZeroFreshVisuals()
	{
		await using var driver = await TransposedGridDriver.CreateAsync(
			ScrollConfig, ScrollColumns, ScrollWindowWidth, ScrollWindowHeight);

		var runner = new PerfScenarioRunner();
		var signals = await runner.MeasureAsync(
			driver.SnapshotScope,
			warmup: async () =>
			{
				// Scroll the full measured range once so the recycle pool reaches steady-state peak
				// realization; end at the low endpoint so the workload's final realized set matches the
				// snapshot instance-for-instance when recycling is clean.
				await driver.ScrollToColumnAsync(ScrollHighColumn);
				await driver.ScrollToColumnAsync(ScrollLowColumn);
			},
			workload: async () =>
			{
				for (var i = 0; i < ScrollRoundTrips; i++)
				{
					await driver.ScrollToColumnAsync(ScrollHighColumn);
					await driver.ScrollToColumnAsync(ScrollLowColumn);
				}
			});

		Report(
			"semistep_scroll_gate.txt",
			$"scroll round-trips: freshVisuals={signals.FreshVisualInstances}  roundTrips={ScrollRoundTrips}  "
			+ $"allocatedBytes={signals.AllocatedBytes:N0}  gen0={signals.Gen0}");

		signals.FreshVisualInstances.Should().Be(
			0,
			"a scrolled viewport must recycle containers in place after warmup; any fresh visual is a subtree "
			+ "rebuild regression");
	}

	[AvaloniaFact(Explicit = true)]
	public async Task ViewportJump_BytesPerColumn_WithinParity_AndBaseline()
	{
		JumpSample transposed;
		await using (var driver = await TransposedGridDriver.CreateAsync(
			JumpConfig, JumpSeed, TransposedWindowWidth, TransposedWindowHeight))
		{
			transposed = await MeasureViewportJumpMedianAsync(driver);
		}

		JumpSample canonical;
		await using (var driver = await CanonicalGridDriver.CreateAsync(
			JumpConfig, JumpSeed, CanonicalWindowWidth, CanonicalWindowHeight))
		{
			canonical = await MeasureViewportJumpMedianAsync(driver);
		}

		var transposedPerColumn = transposed.BytesPerColumn();
		var canonicalPerColumn = canonical.BytesPerColumn();

		Report(
			"semistep_viewport_jump.txt",
			$"viewport-jump transposed: total={transposed.Bytes:N0}  realized={transposed.RealizedColumns}  "
			+ $"per-column={transposedPerColumn:N0}\n"
			+ $"viewport-jump canonical:  total={canonical.Bytes:N0}  realized={canonical.RealizedColumns}  "
			+ $"per-column={canonicalPerColumn:N0}");

		var baselines = PerfBaselines.Load();
		PerfMetricGate.AssertOrRecord(
			_actuals, baselines, _output, PerfMetricNames.TransposedViewportJumpBytesPerColumn, transposedPerColumn);
		PerfMetricGate.AssertOrRecord(
			_actuals, baselines, _output, PerfMetricNames.CanonicalViewportJumpBytesPerColumn, canonicalPerColumn);

		// Both denominators must be non-zero. Guarding only canonical would let a transposed regression that
		// stops realizing columns pass: transposedPerColumn would be 0, so parityRatio 0 <= cap trivially.
		transposed.RealizedColumns.Should().BeGreaterThan(
			0,
			"the transposed jump must realize a viewport of columns; zero realized is a virtualization regression, "
			+ "not a passing parity");
		canonical.RealizedColumns.Should().BeGreaterThan(
			0,
			"the canonical jump must realize a viewport of rows for a meaningful parity denominator");
		transposedPerColumn.Should().BeGreaterThan(
			0,
			"a zero transposed per-column numerator means the jump allocated nothing to realize its viewport; "
			+ "that is a broken measurement, not parity");

		var parityRatio = transposedPerColumn / canonicalPerColumn;
		_output.WriteLine($"[perf] viewport-jump parity ratio (transposed/canonical) = {parityRatio:F2}");

		parityRatio.Should().BeLessThanOrEqualTo(
			ParityRatioLimit,
			$"a recycled transposed column rebind must stay within the honest {ParityRatioLimit}x cap of a "
			+ $"recycled canonical row; tighten this to {ParityRatioTargetAfterHostPoolDeletion}x once "
			+ "TransposedColumnCellsHost + pool are removed");
	}

	[AvaloniaFact(Explicit = true)]
	public async Task PerAdd_ScalesFlat_WithColumnCount()
	{
		var smallSeedPerAdd = await MeasurePerAddAsync(ScalingSmallSeed);
		var largeSeedPerAdd = await MeasurePerAddAsync(ScalingLargeSeed);

		var scalingRatio = smallSeedPerAdd == 0 ? 0 : (double)largeSeedPerAdd / smallSeedPerAdd;

		Report(
			"semistep_per_add_scaling.txt",
			$"per-add N={ScalingSmallSeed}: {smallSeedPerAdd:N0} bytes\n"
			+ $"per-add N={ScalingLargeSeed}: {largeSeedPerAdd:N0} bytes\n"
			+ $"scaling ratio (N={ScalingLargeSeed}/N={ScalingSmallSeed}) = {scalingRatio:F2}");

		var baselines = PerfBaselines.Load();
		PerfMetricGate.AssertOrRecord(
			_actuals, baselines, _output, PerfMetricNames.TransposedPerAddBytesN20, smallSeedPerAdd);
		PerfMetricGate.AssertOrRecord(
			_actuals, baselines, _output, PerfMetricNames.TransposedPerAddBytesN120, largeSeedPerAdd);

		smallSeedPerAdd.Should().BeGreaterThan(
			0,
			"appending a column must allocate something for its realization; a zero denominator is a broken "
			+ "measurement");

		scalingRatio.Should().BeLessThanOrEqualTo(
			ScalingRatioLimit,
			"per-add allocation must stay flat with the existing column count; growth means an append "
			+ "re-touches every column in the recipe, not just the viewport-realized ones");
	}

	private static async Task<JumpSample> MeasureViewportJumpMedianAsync(IRecipeGridDriver driver)
	{
		var samples = new List<JumpSample>(ParitySampleCount);
		for (var i = 0; i < ParitySampleCount; i++)
		{
			samples.Add(await MeasureViewportJumpAsync(driver));
		}

		return samples.OrderBy(sample => sample.BytesPerColumn()).ElementAt(ParitySampleCount / 2);
	}

	// One far viewport jump (start -> end) after a warmup round-trip, so the measured jump reuses recycled
	// containers - the steady-state the app hits when auto-scrolling to a step added far from the viewport.
	private static async Task<JumpSample> MeasureViewportJumpAsync(IRecipeGridDriver driver)
	{
		var lastIndex = driver.ItemCount - 1;
		var runner = new PerfScenarioRunner();
		var signals = await runner.MeasureAsync(
			driver.SnapshotScope,
			warmup: async () =>
			{
				await driver.ScrollToColumnAsync(lastIndex);
				await driver.ScrollToColumnAsync(0);
			},
			workload: () => driver.ScrollToColumnAsync(lastIndex));

		return new JumpSample(signals.AllocatedBytes, driver.RealizedIndices.Count);
	}

	// Per-add bytes at a fixed seed: warm the append+realize path, then measure a fixed run of appends, each
	// followed by a scroll to the new last column so the realization/layout pass is charged to the add.
	private static async Task<long> MeasurePerAddAsync(int seed)
	{
		await using var driver = await TransposedGridDriver.CreateAsync(
			ScalingConfig, seed, ScalingWindowWidth, ScalingWindowHeight);

		var runner = new PerfScenarioRunner();
		var signals = await runner.MeasureAsync(
			driver.SnapshotScope,
			warmup: () => AppendAndRealizeAsync(driver, WarmupAppends),
			workload: () => AppendAndRealizeAsync(driver, MeasuredAppends));

		return signals.AllocatedBytes / MeasuredAppends;
	}

	private static async Task AppendAndRealizeAsync(IRecipeGridDriver driver, int count)
	{
		for (var i = 0; i < count; i++)
		{
			await driver.AddStepsAsync(1);
			await driver.ScrollToColumnAsync(driver.ItemCount - 1);
		}
	}

	private void Report(string fileName, string report)
	{
		_output.WriteLine(report);
		File.WriteAllText(Path.Combine(Path.GetTempPath(), fileName), report);
	}

	private readonly record struct JumpSample(long Bytes, int RealizedColumns)
	{
		public double BytesPerColumn()
		{
			return RealizedColumns == 0 ? 0 : (double)Bytes / RealizedColumns;
		}
	}
}
