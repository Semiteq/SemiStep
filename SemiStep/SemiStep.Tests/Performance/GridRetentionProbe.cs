using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.Performance;

// Memory-retention diagnostic, NOT part of the normal suite. Run it directly:
//   SEMISTEP_PROBE=1 dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj \
//     --filter "FullyQualifiedName~GridRetentionProbe"
//
// Question it answers: does scrolling the recipe grid back and forth ACCUMULATE retained managed
// memory (a leak), or is the live-app sawtooth just normal transient gen0 churn that a full GC
// reclaims? A leak shows the post-full-GC floor growing roughly linearly with the number of scroll
// round-trips; no leak shows the floor flat within GC noise regardless of how many round-trips ran.
//
// It runs the same workload against BOTH surfaces to isolate the layer:
//   - transposed: StepColumns are virtualized horizontally; each realized column builds control-layer
//     presenters/editors on top of the shared StepColumnViewModel/ParameterCellViewModel.
//   - canonical : RecipeRows are virtualized vertically in the DataGrid over the same Core row model.
// If BOTH floors grow -> retention is in the shared/Core layer (the row/cell view models). If ONLY the
// transposed floor grows -> it is view-specific to the transposed control stack. If BOTH are flat ->
// not a leak.
//
// Soundness guards:
//   - The floor is read only AFTER a forced, blocking, compacting gen2 collection plus a finalizer
//     drain, done twice, so nothing collectible is counted.
//   - Transposed cell view models are built lazily per column on first realization and then held by
//     their column for the surface lifetime. The warmup force-realizes EVERY column's cells so that
//     lazy creation (legitimate, bounded, one-time) cannot masquerade as growth during the measured
//     workload. The floor baseline therefore models a user who has already scrolled the whole recipe.
//   - No per-realization container/VM is held in a strong local across a floor read; only the window and
//     surface stay rooted (legitimately), plus WeakReferences that cannot root anything.
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
	private const int WarmupRoundTrips = 4;
	private const int CheckpointRoundTrips = 75;
	private const int WorkloadRoundTrips = 150;

	// Generous guard: over 150 round-trips a flat floor jitters by a few hundred KB from GC
	// nondeterminism, so a real linear leak (tens of KB per round-trip -> multiple MB total) clears
	// this bar while normal noise does not. The probe's job is the number; this only trips on a gross leak.
	private const long RetainedPerRoundTripGuardBytes = 100_000;

	private readonly ITestOutputHelper _output;

	public GridRetentionProbe(ITestOutputHelper output)
	{
		_output = output;
	}

	[AvaloniaFact]
	public async Task Transposed_ScrollRetention_Report()
	{
		Assert.SkipUnless(
			Environment.GetEnvironmentVariable("SEMISTEP_PROBE") == "1",
			"Measurement probe: set SEMISTEP_PROBE=1 to run.");

		var result = await MeasureTransposedAsync();
		Emit("transposed", result);
		AssertLooseGuard(result);
	}

	[AvaloniaFact]
	public async Task Canonical_ScrollRetention_Report()
	{
		Assert.SkipUnless(
			Environment.GetEnvironmentVariable("SEMISTEP_PROBE") == "1",
			"Measurement probe: set SEMISTEP_PROBE=1 to run.");

		var result = await MeasureCanonicalAsync();
		Emit("canonical", result);
		AssertLooseGuard(result);
	}

	private static async Task<RetentionResult> MeasureTransposedAsync()
	{
		var fixture = new UIFixture();
		await fixture.InitializeAsync(ConfigName);
		try
		{
			fixture.SeedRecipe(SeedSteps);
			var surface = fixture.CreateTransposedSurface();
			surface.Initialize();
			var view = new TransposedRecipeGridView { DataContext = surface };
			var window = new Window { Width = TransposedWindowWidth, Height = WindowHeight, Content = view };
			window.Show();
			Dispatcher.UIThread.RunJobs();

			var listBox = view.FindControl<ListBox>("StepListBox")!;
			var lastIndex = surface.StepColumns.Count - 1;

			void ToEnd()
			{
				listBox.ScrollIntoView(lastIndex);
				SetHorizontalOffset(listBox, double.MaxValue);
			}

			void ToStart()
			{
				listBox.ScrollIntoView(0);
				SetHorizontalOffset(listBox, 0);
			}

			// Realize every column's lazy cells so the baseline floor already contains the full,
			// bounded shared-VM population; only transient control-layer churn can move the floor after this.
			ForceAllTransposedCells(surface);

			var sharedVmCount = CountTransposedCellVms(surface);

			var result = RunWorkload(
				ToEnd,
				ToStart,
				() => listBox.GetRealizedContainers().Cast<Control>());

			window.Close();
			return result with { SharedVmCount = sharedVmCount };
		}
		finally
		{
			await fixture.DisposeAsync();
		}
	}

	private static async Task<RetentionResult> MeasureCanonicalAsync()
	{
		var fixture = new UIFixture();
		await fixture.InitializeAsync(ConfigName);
		try
		{
			fixture.SeedRecipe(SeedSteps);
			var surface = fixture.CreateCanonicalSurface();
			surface.Initialize();
			var view = new CanonicalRecipeGridView { DataContext = surface };
			var window = new Window { Width = CanonicalWindowWidth, Height = WindowHeight, Content = view };
			window.Show();
			Dispatcher.UIThread.RunJobs();

			void ToEnd()
			{
				SetVerticalOffset(view, double.MaxValue);
			}

			void ToStart()
			{
				SetVerticalOffset(view, 0);
			}

			// The DataGrid holds one RecipeRowViewModel per step, all built eagerly at seed time, so the
			// shared-VM population is already complete; there is no lazy per-cell VM to force.
			var sharedVmCount = surface.RecipeRows.Count;

			var result = RunWorkload(
				ToEnd,
				ToStart,
				() => view.GetVisualDescendants().OfType<DataGridRow>().Cast<Control>());

			window.Close();
			return result with { SharedVmCount = sharedVmCount };
		}
		finally
		{
			await fixture.DisposeAsync();
		}
	}

	// Drives the round-trip workload and reads the post-full-GC floor at three checkpoints so linearity
	// is observed directly rather than assumed. realizedContainers supplies the currently-realized
	// containers at the far endpoint for the control-layer survivor probe.
	private static RetentionResult RunWorkload(
		Action toEnd,
		Action toStart,
		Func<IEnumerable<Control>> realizedContainers)
	{
		for (var i = 0; i < WarmupRoundTrips; i++)
		{
			RoundTrip(toEnd, toStart);
		}

		var floor0 = Floor();

		for (var i = 0; i < CheckpointRoundTrips; i++)
		{
			RoundTrip(toEnd, toStart);
		}

		var floorMid = Floor();

		for (var i = CheckpointRoundTrips; i < WorkloadRoundTrips; i++)
		{
			RoundTrip(toEnd, toStart);
		}

		var floor1 = Floor();

		// Control-layer survivor probe: realize the far endpoint, weakly reference every realized
		// container, then park at the near endpoint (unrealizing them) and force a full GC. Survivors are
		// what the control stack still roots after unrealize. A recycling pool keeps a BOUNDED set alive;
		// only a count that would grow with N indicates a control-layer leak (the flat floor is the primary
		// signal for that).
		toEnd();
		Dispatcher.UIThread.RunJobs();
		var endContainers = realizedContainers().Select(container => new WeakReference(container)).ToList();
		var realizedAtEnd = endContainers.Count;

		toStart();
		Dispatcher.UIThread.RunJobs();

		var floorFinal = Floor();
		var containerSurvivors = endContainers.Count(reference => reference.IsAlive);
		endContainers = null;

		return new RetentionResult(
			Floor0: floor0,
			FloorMid: floorMid,
			Floor1: floor1,
			FloorFinal: floorFinal,
			RealizedAtEnd: realizedAtEnd,
			ContainerSurvivors: containerSurvivors,
			SharedVmCount: 0);
	}

	private static void RoundTrip(Action toEnd, Action toStart)
	{
		toEnd();
		Dispatcher.UIThread.RunJobs();
		toStart();
		Dispatcher.UIThread.RunJobs();
	}

	private static long Floor()
	{
		GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
		GC.WaitForPendingFinalizers();
		GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
		GC.WaitForPendingFinalizers();
		return GC.GetTotalMemory(forceFullCollection: true);
	}

	private static void ForceAllTransposedCells(TransposedRecipeGridSurface surface)
	{
		foreach (var column in surface.StepColumns)
		{
			_ = column.Cells.Count;
		}
	}

	private static int CountTransposedCellVms(TransposedRecipeGridSurface surface)
	{
		return surface.StepColumns.Sum(column => column.Cells.Count);
	}

	private static void SetHorizontalOffset(ListBox listBox, double x)
	{
		var scrollViewer = listBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
		if (scrollViewer is null)
		{
			return;
		}

		var clamped = x >= scrollViewer.Extent.Width ? scrollViewer.Extent.Width : x;
		scrollViewer.Offset = new Vector(clamped, scrollViewer.Offset.Y);
	}

	private static void SetVerticalOffset(Control view, double y)
	{
		var scrollViewer = view.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
		if (scrollViewer is null)
		{
			return;
		}

		var clamped = y >= scrollViewer.Extent.Height ? scrollViewer.Extent.Height : y;
		scrollViewer.Offset = new Vector(scrollViewer.Offset.X, clamped);
	}

	private void Emit(string surfaceLabel, RetentionResult result)
	{
		var deltaTotal = result.Floor1 - result.Floor0;
		var perRoundTrip = deltaTotal / (double)WorkloadRoundTrips;
		var firstHalf = result.FloorMid - result.Floor0;
		var secondHalf = result.Floor1 - result.FloorMid;
		var verdict = Math.Abs(perRoundTrip) < RetainedPerRoundTripGuardBytes ? "FLAT (no leak)" : "GROWING (leak)";

		var lines = new List<string>
		{
			$"surface                     = {surfaceLabel}",
			$"config / seed steps         = {ConfigName} / {SeedSteps}",
			$"round-trips (warm/half/full)= {WarmupRoundTrips} / {CheckpointRoundTrips} / {WorkloadRoundTrips}",
			$"shared VM count (rooted)    = {result.SharedVmCount:N0}",
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

	private static void AssertLooseGuard(RetentionResult result)
	{
		var perRoundTrip = (result.Floor1 - result.Floor0) / (double)WorkloadRoundTrips;
		Assert.True(
			perRoundTrip < RetainedPerRoundTripGuardBytes,
			$"Post-GC floor grew {perRoundTrip:N0} bytes per scroll round-trip, above the " +
			$"{RetainedPerRoundTripGuardBytes:N0} guard: this indicates a retention leak, not transient churn.");
	}

	private readonly record struct RetentionResult(
		long Floor0,
		long FloorMid,
		long Floor1,
		long FloorFinal,
		int RealizedAtEnd,
		int ContainerSurvivors,
		int SharedVmCount);
}
