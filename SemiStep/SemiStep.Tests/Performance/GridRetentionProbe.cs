using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Headless.XUnit;

using SemiStep.Tests.Performance.Harness;

using Xunit;

namespace SemiStep.Tests.Performance;

// Memory-retention gate, migrated onto the black-box harness. Explicit measurement fact: plain
// `dotnet test`/CI does not run it (xunit v3 Explicit). Run:
//   SemiStep/Artifacts/bin/SemiStep.Tests/<config>/SemiStep.Tests.exe \
//     -explicit only -method "*GridRetentionProbe*"
//
// Question it answers: does scrolling the recipe grid back and forth ACCUMULATE retained managed
// memory (a leak), or is the live-app sawtooth just normal transient gen0 churn that a full GC
// reclaims? A leak shows the post-full-GC floor growing roughly linearly with the number of scroll
// round-trips; no leak shows the floor flat within GC noise regardless of how many round-trips ran.
//
// It runs the SAME scenario against BOTH drivers to isolate the layer:
//   - transposed: StepColumns are virtualized horizontally; each realized column builds control-layer
//     presenters/editors on top of the shared StepColumnViewModel/ParameterCellViewModel.
//   - canonical : RecipeRows are virtualized vertically in the DataGrid over the same Core row model.
// If BOTH floors grow -> retention is in the shared/Core layer (the row/cell view models). If ONLY the
// transposed floor grows -> it is view-specific to the transposed control stack. If BOTH are flat ->
// not a leak.
//
// Gate hierarchy realized here:
//   - PRIMARY: the two-point flat-delta invariant. The retained floor is sampled (SampleRetainedFloorAsync:
//     settle jobs -> full blocking compacting GC -> GC.GetTotalMemory(true)) BEFORE and AFTER N workload
//     cycles; the delta per round-trip must stay under the flat-delta guard. This is machine-independent:
//     it compares two floors from one process, so absolute heap size cancels out.
//   - TELEMETRY only: the absolute floor is reported to the actuals fixture and baseline-gated once
//     captured (assert-or-record). A 20% tolerance on a ~100 MB floor would hide a slow leak, so the
//     absolute floor can never be the primary gate.
//   - Control-layer survivor probe (preserved as-is): weak-references every container realized at the far
//     endpoint, parks the viewport away, forces a full GC, and counts survivors. A recycling pool keeps a
//     BOUNDED set alive; only a count that grows with N is a control-layer leak.
//
// Soundness guards:
//   - The floor is read only AFTER a forced, blocking, compacting gen2 collection plus a finalizer
//     drain, done twice, so nothing collectible is counted (PerfScenarioRunner.SampleRetainedFloorAsync).
//   - Transposed cell view models are built lazily per column on first realization and then held by
//     their column for the surface lifetime. The warmup scrolls the FULL range once so lazy creation
//     (legitimate, bounded, one-time) is already paid before floor0 and cannot masquerade as growth
//     during the measured workload. The baseline floor therefore models a user who has already scrolled
//     the whole recipe.
//   - No per-realization container/VM is held in a strong local across a floor read; only the driver's
//     window and surface stay rooted (legitimately), plus WeakReferences that cannot root anything.
[Trait("Category", "Performance")]
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
public sealed class GridRetentionProbe
{
	private const string ConfigName = "WideParams";
	private const int SeedSteps = 300;
	private const int TransposedWindowWidth = 1400;
	private const int CanonicalWindowWidth = 1600;
	private const int WindowHeight = 800;
	private const int CheckpointRoundTrips = 75;
	private const int WorkloadRoundTrips = 150;

	// Flat-delta guard. Over 150 round-trips a genuinely flat floor moves only a few thousand bytes per
	// round-trip from GC nondeterminism (measured: transposed ~-1.4 KB, canonical up to ~6 KB), so this
	// ~20 KB bar sits ~3x above the largest observed |delta| yet still below a real linear leak of tens of
	// KB per round-trip (-> multiple MB total), which trips it. Normal noise does not.
	private const long RetainedPerRoundTripGuardBytes = 20_000;

	// Absolute viewport-derived cap on control-layer survivors, independent of how many containers were
	// realized at the endpoint (bounding by that count is a tautology - survivors are a subset of it). A
	// healthy recycling pool keeps roughly a viewport-worth alive (measured: transposed ~16, canonical ~32);
	// this cap sits above the larger with headroom and far under the 300-step recipe, so it trips only if the
	// control stack roots well past a viewport (a leak that keeps scrolled-history containers alive).
	private const int MaxSurvivingContainers = 64;

	private readonly ITestOutputHelper _output;
	private readonly PerfActualsFixture _actuals;

	public GridRetentionProbe(ITestOutputHelper output, PerfActualsFixture actuals)
	{
		_output = output;
		_actuals = actuals;
	}

	[AvaloniaFact(Explicit = true)]
	public async Task Transposed_ScrollRetention_FlatFloor()
	{
		await using var driver = await TransposedGridDriver.CreateAsync(
			ConfigName, SeedSteps, TransposedWindowWidth, WindowHeight);

		var result = await MeasureRetentionAsync(driver);
		Emit("transposed", result);
		AssertFlatDelta("transposed", result);
		AssertBoundedSurvivors("transposed", result);

		// Absolute floor is telemetry only: a 20% tolerance on a ~40 MB floor would hide a slow leak, so the
		// flat-delta invariant and the survivor cap above are this probe's hard gates. Report it, never fail on it.
		PerfMetricGate.RecordAdvisory(
			_actuals, PerfBaselines.Load(), _output, PerfMetricNames.TransposedRetentionFloorBytes, result.Floor1);
	}

	[AvaloniaFact(Explicit = true)]
	public async Task Canonical_ScrollRetention_FlatFloor()
	{
		await using var driver = await CanonicalGridDriver.CreateAsync(
			ConfigName, SeedSteps, CanonicalWindowWidth, WindowHeight);

		var result = await MeasureRetentionAsync(driver);
		Emit("canonical", result);
		AssertFlatDelta("canonical", result);
		AssertBoundedSurvivors("canonical", result);

		PerfMetricGate.RecordAdvisory(
			_actuals, PerfBaselines.Load(), _output, PerfMetricNames.CanonicalRetentionFloorBytes, result.Floor1);
	}

	// Runs the identical round-trip workload against either driver and reads the post-full-GC floor at
	// two checkpoints plus a mid-point (so linearity is observed directly rather than assumed), then runs
	// the control-layer survivor probe at the far endpoint.
	private static async Task<RetentionResult> MeasureRetentionAsync(IRecipeGridDriver driver)
	{
		var runner = new PerfScenarioRunner();
		var lastIndex = driver.ItemCount - 1;

		// Realize every item once so the bounded, one-time lazy population is already paid before floor0;
		// only transient control-layer churn can move the floor after this.
		for (var index = 0; index <= lastIndex; index++)
		{
			await driver.ScrollToColumnAsync(index);
		}

		await driver.ScrollToColumnAsync(0);

		var floor0 = await runner.SampleRetainedFloorAsync();

		for (var i = 0; i < CheckpointRoundTrips; i++)
		{
			await RoundTripAsync(driver, lastIndex);
		}

		var floorMid = await runner.SampleRetainedFloorAsync();

		for (var i = CheckpointRoundTrips; i < WorkloadRoundTrips; i++)
		{
			await RoundTripAsync(driver, lastIndex);
		}

		var floor1 = await runner.SampleRetainedFloorAsync();

		// Control-layer survivor probe: realize the far endpoint, weakly reference every realized
		// container, then park at the near endpoint (unrealizing them) and force a full GC. Survivors are
		// what the control stack still roots after unrealize. Only weak references outlive this scope, so
		// nothing here roots a container across the floor read.
		await driver.ScrollToColumnAsync(lastIndex);
		var endContainers = driver.RealizedContainers
			.Select(container => new WeakReference(container))
			.ToList();
		var realizedAtEnd = endContainers.Count;

		await driver.ScrollToColumnAsync(0);

		var floorFinal = await runner.SampleRetainedFloorAsync();
		var containerSurvivors = endContainers.Count(reference => reference.IsAlive);
		endContainers = null;

		return new RetentionResult(
			Floor0: floor0,
			FloorMid: floorMid,
			Floor1: floor1,
			FloorFinal: floorFinal,
			RealizedAtEnd: realizedAtEnd,
			ContainerSurvivors: containerSurvivors);
	}

	private static async Task RoundTripAsync(IRecipeGridDriver driver, int lastIndex)
	{
		await driver.ScrollToColumnAsync(lastIndex);
		await driver.ScrollToColumnAsync(0);
	}

	private void Emit(string surfaceLabel, RetentionResult result)
	{
		var deltaTotal = result.Floor1 - result.Floor0;
		var perRoundTrip = deltaTotal / (double)WorkloadRoundTrips;
		var firstHalf = result.FloorMid - result.Floor0;
		var secondHalf = result.Floor1 - result.FloorMid;
		// Verdict must track the gate (which fails only on positive growth past the guard): a floor that
		// shrank past the guard is not a leak, so it reads FLAT rather than GROWING.
		var verdict = perRoundTrip >= RetainedPerRoundTripGuardBytes
			? "GROWING (leak)"
			: perRoundTrip <= -RetainedPerRoundTripGuardBytes
				? "SHRANK (no leak)"
				: "FLAT (no leak)";

		var lines = new List<string>
		{
			$"surface                     = {surfaceLabel}",
			$"config / seed steps         = {ConfigName} / {SeedSteps}",
			$"round-trips (half/full)     = {CheckpointRoundTrips} / {WorkloadRoundTrips}",
			$"floor @0   round-trips      = {result.Floor0,15:N0} bytes",
			$"floor @{CheckpointRoundTrips,-3} round-trips      = {result.FloorMid,15:N0} bytes  (+{firstHalf,13:N0} vs @0)",
			$"floor @{WorkloadRoundTrips,-3} round-trips      = {result.Floor1,15:N0} bytes  (+{secondHalf,13:N0} vs @{CheckpointRoundTrips})",
			$"floor after park+GC         = {result.FloorFinal,15:N0} bytes",
			$"retained delta @0..@{WorkloadRoundTrips}      = {deltaTotal,15:N0} bytes total",
			$"retained per round-trip     = {perRoundTrip,15:N0} bytes",
			$"realized containers @end    = {result.RealizedAtEnd}",
			$"container survivors @start  = {result.ContainerSurvivors}  (bounded pool is normal)",
			$"VERDICT                     = {verdict}",
			string.Empty,
		};

		var report = string.Join(Environment.NewLine, lines);
		_output.WriteLine(report);

		var path = Path.Combine(Path.GetTempPath(), "semistep_retention_probe.txt");
		File.AppendAllText(path, report + Environment.NewLine);
	}

	// PRIMARY gate: the two-point flat-delta invariant. Machine-independent (two floors from one process).
	private static void AssertFlatDelta(string label, RetentionResult result)
	{
		var perRoundTrip = (result.Floor1 - result.Floor0) / (double)WorkloadRoundTrips;
		Assert.True(
			perRoundTrip < RetainedPerRoundTripGuardBytes,
			$"{label}: post-GC retained floor grew {perRoundTrip:N0} bytes per scroll round-trip over " +
			$"{WorkloadRoundTrips} cycles (floor {result.Floor0:N0} -> {result.Floor1:N0}), above the " +
			$"{RetainedPerRoundTripGuardBytes:N0} flat-delta guard: this is a retention leak, not transient churn.");
	}

	// Control-layer survivor gate (preserved from the original probe, now a real bound). The realized
	// viewport must stay far under the full step count (virtualization working), and the containers the
	// control stack still roots after the viewport is parked away must stay under an absolute viewport-derived
	// cap. Bounding survivors by RealizedAtEnd would be a tautology (survivors are counted from exactly those
	// realized-at-end references, so the subset can never exceed it); the fixed cap is the actual gate - a
	// control-layer leak that roots scrolled-history containers blows past a viewport-worth.
	private static void AssertBoundedSurvivors(string label, RetentionResult result)
	{
		Assert.True(
			result.RealizedAtEnd < SeedSteps,
			$"{label}: {result.RealizedAtEnd} containers realized at the endpoint of a {SeedSteps}-step recipe; " +
			"virtualization must keep realization viewport-bound, well under the full count.");
		Assert.True(
			result.ContainerSurvivors <= MaxSurvivingContainers,
			$"{label}: {result.ContainerSurvivors} container survivors exceed the {MaxSurvivingContainers} " +
			"viewport-derived cap after parking the viewport away; the control stack is rooting more than a " +
			"recycling pool's bounded viewport-worth (a control-layer retention leak).");
	}

	private readonly record struct RetentionResult(
		long Floor0,
		long FloorMid,
		long Floor1,
		long FloorFinal,
		int RealizedAtEnd,
		int ContainerSurvivors);
}
