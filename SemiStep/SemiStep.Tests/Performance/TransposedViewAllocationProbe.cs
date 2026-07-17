using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Tests.Performance.Harness;

using Xunit;

namespace SemiStep.Tests.Performance;

// Black-box scroll/recycle gates, migrated onto the harness (PerfScenarioRunner + IRecipeGridDriver +
// PerfBaselines). Every assertion is expressed over framework-boundary signals or driver-agnostic ratios,
// so a panel-implementation refactor cannot break a gate that still holds the real invariant. Replaces the
// old white-box host-reattach counter (which named TransposedColumnCellsHost) with FreshVisualInstances==0.
//
// These are explicit measurement facts: plain `dotnet test`/CI does not run them (xunit v3 Explicit). Run:
//   SemiStep/Artifacts/bin/SemiStep.Tests/<config>/SemiStep.Tests.exe -explicit only -method "*AllocationProbe*"
//
// Gate hierarchy realized here:
//   - FreshVisualInstances == 0 on scroll round-trips: an exact, cross-machine invariant, asserted in code.
//   - transposed/canonical viewport-jump parity ratio <= 3.3x: a same-scenario cross-machine ratio gate.
//     Honest current-architecture cap (~3.0x measured); tightens to 2.0x once the Host + pool are deleted.
//   - per-add scaling ratio (N=120 vs N=20) <= 1.5: the flat-growth invariant.
//   - viewport-jump bytes/column absolute: soft telemetry, baseline-gated once captured (records-only until).
//
// The absolute-byte compares are assert-or-record: they hard-fail on regression once Docs/perf/baselines.json
// carries the metric, and record-only (report to the actuals fixture, no assert) while it is absent.
// Committing the captured numbers to the baseline flips them to hard gates; the invariant and ratio gates
// above never depend on a baseline.
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

	// Honest current-architecture cap: a recycled transposed column rebind measures ~3.0x a canonical row while
	// the TransposedColumnCellsHost + pool indirection is still present. The cap is set ~10% over that
	// worst-case so it stays a HARD regression gate. It tightens to 2.0x once the host + pool are removed - that
	// removal drops the extra per-cell allocation the 3x reflects.
	private const double ParityRatioLimit = 3.3;
	private const double ParityRatioTargetAfterHostPoolDeletion = 2.0;

	// Per-add scaling: per-add bytes at a large seed vs a small seed. Realization is viewport-bound and about
	// equal at both seeds, so the per-add cost stays near 1:1 across N; the ratio grows only if an append
	// re-touches every column in the recipe (an O(total-column) regression), which this gate catches.
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

	// Scroll round-trips over a virtualized viewport must build zero new visuals: after warmup pre-fills the
	// recycle pool at both endpoints, every subsequent scroll reuses container instances. This is the
	// black-box replacement for the deleted host-reattach counter and catches ANY newly-created control in
	// the items-panel subtree, not just a named host type.
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

	// Viewport jump: bytes per realized container on a single far jump, measured identically on the
	// transposed and the canonical driver. The transposed absolute is soft-telemetry (assert-or-record); the
	// transposed/canonical parity ratio is the hard cross-machine gate.
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

	// Per-add scaling: per-add bytes must not grow with the existing column count. The ratio of per-add bytes
	// at N=120 vs N=20 is the flat-growth invariant; the absolute per-add values are soft telemetry.
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

	// Median of ParitySampleCount viewport jumps (by per-column bytes) so the parity ratio is not decided by
	// a single jittery shot on its ~10% margin.
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
