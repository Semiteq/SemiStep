using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.Performance;

// Manual measurement tool, NOT part of the normal suite. Run it directly with the real headless view
// realized in a window so the layout/realization pass is included in the measurement:
//   SEMISTEP_PROBE=1 dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj \
//     --filter "FullyQualifiedName~TransposedViewAllocationProbe"
//
// It answers the ONE routing question that a gcdump cannot: does the per-add allocation churn GROW
// with the existing step count N? A gcdump forces a full GC before capturing, so it only ever shows
// retained/live objects — it structurally erases the transient per-add churn and the layout CPU cost,
// which is exactly what "тормозит / 50-100 MB per column" describes. This probe measures the
// thread-local bytes allocated by one AppendStep INCLUDING the RunJobs layout pass that realizes the
// new column, at growing N, for both the transposed and the canonical view.
//
// Two scenarios are covered:
//   - WithGroups (5 cells/column) — the small baseline the prior analysis decomposed.
//   - WideParams (~36 cells/column, several combo + many text editors) — real-scale, where the
//     per-column realization cost and the viewport-jump cost dominate.
//
// Beyond the per-add sweep it also measures the "viewport jump": seed N, realize the horizontal start,
// then measure the allocation of a single ScrollIntoView(last) that jumps the viewport start->end.
// This reproduces the real "add step while scrolled far away" the app performs (auto-scroll to the
// inserted step realizes a full viewport of columns in one dispatcher frame), which the one-column
// per-add shift does NOT.
//
// Reading the result (written to %TEMP%/semistep_ui_probe.txt):
//   - transposed per-add roughly CONSTANT across N  -> churn is one column's realization; the
//     50-100 MB lives in render/composition (headless-invisible) -> capture a Rider Timeline snapshot.
//   - transposed per-add GROWS with N               -> an append re-touches all realized columns/cells;
//     that is the bug, and it is fixable in managed layout/binding code.
//   - canonical per-add stays flat while transposed climbs -> confirms the transposed-view-specific
//     regression the user reports ("на конвенциальной таблице проблем нет").
//   - viewport-jump per-realized-column is the primary success metric: the target is <= ~2x the
//     canonical recycled-row cost once the container-reuse fix lands.
[Trait("Category", "Performance")]
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
public sealed class TransposedViewAllocationProbe
{
	private const int WarmupAppends = 6;
	private const int MeasuredAppends = 12;

	// Host-reattach probe: enough columns to scroll ~200 apart in a 1400px window.
	private const string HostReattachConfig = "WideParams";
	private const int HostReattachWindowWidth = 1400;
	private const int HostReattachColumns = 420;
	private const int HostReattachLowColumn = 20;
	private const int HostReattachHighColumn = 220;
	private const int HostReattachRoundTrips = 20;

	private static readonly int[] _seedSizes = { 20, 60, 120 };

	// Narrow window for WithGroups keeps the horizontal panel virtualizing; the wide config uses a
	// wider window so a viewport jump realizes a realistic ~20-25 columns in one frame.
	private static readonly ProbeScenario[] _scenarios =
	{
		new(Label: "WithGroups", ConfigName: "WithGroups", WindowWidth: 560),
		new(Label: "WideParams", ConfigName: "WideParams", WindowWidth: 1400),
	};

	private readonly ITestOutputHelper _output;

	public TransposedViewAllocationProbe(ITestOutputHelper output)
	{
		_output = output;
	}

	[AvaloniaFact]
	public async Task Report_PerAdd_ViewAllocation()
	{
		Assert.SkipUnless(
			Environment.GetEnvironmentVariable("SEMISTEP_PROBE") == "1",
			"Measurement probe: set SEMISTEP_PROBE=1 to run.");

		var lines = new List<string>();

		foreach (var scenario in _scenarios)
		{
			foreach (var seed in _seedSizes)
			{
				var sample = await MeasureAsync(scenario, seed, transposed: true);
				lines.Add(
					$"transposed {scenario.Label,-11} N={seed,4}  per-add(+realize) = {sample.BytesPerAdd,13:N0} bytes  " +
					$"gen0/add = {sample.Gen0PerAdd:F2}");
			}

			foreach (var seed in _seedSizes)
			{
				var sample = await MeasureAsync(scenario, seed, transposed: false);
				lines.Add(
					$"canonical  {scenario.Label,-11} N={seed,4}  per-add(+realize) = {sample.BytesPerAdd,13:N0} bytes  " +
					$"gen0/add = {sample.Gen0PerAdd:F2}");
			}

			lines.Add(await DescribeRealizedColumnAsync(scenario, 60));

			var jump = await MeasureViewportJumpAsync(scenario, 120);
			var perColumn = jump.RealizedColumns == 0 ? 0 : jump.TotalBytes / jump.RealizedColumns;
			lines.Add(
				$"viewport-jump {scenario.Label,-11} N=120  total = {jump.TotalBytes,13:N0} bytes  " +
				$"realized-cols = {jump.RealizedColumns,3}  per-realized-col = {perColumn,11:N0} bytes");

			lines.Add(string.Empty);
		}

		var report = string.Join(Environment.NewLine, lines);
		_output.WriteLine(report);
		File.WriteAllText(Path.Combine(Path.GetTempPath(), "semistep_ui_probe.txt"), report);
	}

	// Host re-attach gate: during a scripted ~200-column scroll after warmup, counts how many FRESH
	// TransposedColumnCellsHost instances get built. Pre-fix each recycle discards and rebuilds the host
	// subtree, so ~36 new hosts appear per round-trip (see TransposedScrollTraceScenario.Report_HostReattachBaseline).
	// Post-fix the child is recycled in place, hosts persist, and steady-state scroll builds zero new hosts.
	//
	// Discriminating metric = new host instances after warmup, NOT AttachedToVisualTree on pre-realized
	// hosts: pre-fix the host instance is REPLACED (not re-attached), so subscribing AttachedToVisualTree on
	// the warmed-up hosts fires 0 either way. attachFirings is reported for context only. The public
	// AttachedToVisualTree subscription (same technique the contract test uses for DetachedFromVisualTree)
	// still catches any re-attach of a persisted host, which post-fix must also be 0.
	[AvaloniaFact]
	public async Task Report_HostReattach_IsZeroAfterFix()
	{
		Assert.SkipUnless(
			Environment.GetEnvironmentVariable("SEMISTEP_PROBE") == "1",
			"Measurement probe: set SEMISTEP_PROBE=1 to run.");

		var fixture = new UIFixture();
		await fixture.InitializeAsync(HostReattachConfig);
		try
		{
			fixture.SeedRecipe(HostReattachColumns);
			var surface = fixture.CreateTransposedSurface();
			surface.Initialize();
			var view = new TransposedRecipeGridView { DataContext = surface };
			var window = new Window { Width = HostReattachWindowWidth, Height = 800, Content = view };
			window.Show();
			Dispatcher.UIThread.RunJobs();

			var listBox = view.FindControl<ListBox>("StepListBox")!;

			// Warm one round-trip so both scroll endpoints have realized before the seen-set snapshot.
			listBox.ScrollIntoView(HostReattachHighColumn);
			Dispatcher.UIThread.RunJobs();
			listBox.ScrollIntoView(HostReattachLowColumn);
			Dispatcher.UIThread.RunJobs();

			var seenHosts = new HashSet<TransposedColumnCellsHost>(ReferenceEqualityComparer.Instance);
			var attachFirings = 0;
			var newHostInstances = 0;

			void Discover()
			{
				foreach (var host in listBox.GetVisualDescendants().OfType<TransposedColumnCellsHost>())
				{
					if (seenHosts.Add(host))
					{
						newHostInstances++;
						host.AttachedToVisualTree += (_, _) => attachFirings++;
					}
				}
			}

			// Seed the seen-set with warmed-up hosts; these are not scroll-driven rebuilds.
			Discover();
			newHostInstances = 0;

			for (var i = 0; i < HostReattachRoundTrips; i++)
			{
				listBox.ScrollIntoView(HostReattachHighColumn);
				Dispatcher.UIThread.RunJobs();
				Discover();

				listBox.ScrollIntoView(HostReattachLowColumn);
				Dispatcher.UIThread.RunJobs();
				Discover();
			}

			window.Close();

			var report =
				$"host-reattach after-fix: newHostInstances = {newHostInstances}  " +
				$"attachFirings(tracked) = {attachFirings}  roundTrips = {HostReattachRoundTrips}  " +
				$"new-hosts/roundtrip = {(double)newHostInstances / HostReattachRoundTrips:F1}";
			_output.WriteLine(report);
			File.WriteAllText(Path.Combine(Path.GetTempPath(), "semistep_host_reattach_after.txt"), report);

			newHostInstances.Should().Be(
				0,
				"the recycle-in-place fix reuses host instances, so steady-state scroll builds no fresh hosts");
			attachFirings.Should().Be(
				0,
				"persisted hosts stay attached across scroll, so AttachedToVisualTree must not re-fire");
		}
		finally
		{
			await fixture.DisposeAsync();
		}
	}

	// Counts the live editor controls a single realized transposed column instantiates, to size the
	// always-live-editor cost: every ComboBox/TextBox is a full control, a TextBlock is cheap.
	private static async Task<string> DescribeRealizedColumnAsync(ProbeScenario scenario, int seed)
	{
		var fixture = new UIFixture();
		await fixture.InitializeAsync(scenario.ConfigName);
		try
		{
			fixture.SeedRecipe(seed);
			var surface = fixture.CreateTransposedSurface();
			surface.Initialize();
			var view = new TransposedRecipeGridView { DataContext = surface };
			var window = new Window { Width = scenario.WindowWidth, Height = 800, Content = view };
			window.Show();
			Dispatcher.UIThread.RunJobs();

			var listBox = view.FindControl<ListBox>("StepListBox")!;
			var container = (ListBoxItem)listBox.ContainerFromIndex(0)!;
			var descendants = container.GetVisualDescendants().ToList();
			var comboBoxes = descendants.OfType<ComboBox>().Count();
			var textBoxes = descendants.OfType<TextBox>().Count();
			var textBlocks = descendants.OfType<TextBlock>().Count();

			window.Close();

			return $"one realized column {scenario.Label}: cells={surface.StepColumns[0].Cells.Count}  " +
				$"ComboBox={comboBoxes}  TextBox={textBoxes}  TextBlock={textBlocks}";
		}
		finally
		{
			await fixture.DisposeAsync();
		}
	}

	private static async Task<Sample> MeasureAsync(ProbeScenario scenario, int seed, bool transposed)
	{
		var fixture = new UIFixture();
		await fixture.InitializeAsync(scenario.ConfigName);
		try
		{
			fixture.SeedRecipe(seed);

			Control view;
			Action realize;
			if (transposed)
			{
				var surface = fixture.CreateTransposedSurface();
				surface.Initialize();
				var transposedView = new TransposedRecipeGridView { DataContext = surface };
				view = transposedView;
				// Scroll the ListBox's OWN (horizontal) scroll viewer to the end so the freshly
				// appended column realizes. The outer UserControl scroll viewer is vertical and would
				// leave the new column virtualized off-screen, measuring nothing.
				realize = () => ScrollListBoxToEnd(transposedView, surface.StepColumns.Count);
			}
			else
			{
				var surface = fixture.CreateCanonicalSurface();
				surface.Initialize();
				view = new CanonicalRecipeGridView { DataContext = surface };
				realize = () => ScrollToEnd(view);
			}

			var window = new Window { Width = scenario.WindowWidth, Height = 800, Content = view };
			window.Show();
			Dispatcher.UIThread.RunJobs();

			// Warm the JIT, the template-application, and the virtualization paths so the measured
			// samples are steady-state and the first-realize JIT cost is not charged to add #1.
			for (var i = 0; i < WarmupAppends; i++)
			{
				AppendAndRealize(fixture, realize);
			}

			var gen0Before = GC.CollectionCount(0);
			var bytesBefore = GC.GetAllocatedBytesForCurrentThread();

			for (var i = 0; i < MeasuredAppends; i++)
			{
				AppendAndRealize(fixture, realize);
			}

			var bytes = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
			var gen0 = GC.CollectionCount(0) - gen0Before;

			window.Close();

			return new Sample(bytes / MeasuredAppends, (double)gen0 / MeasuredAppends);
		}
		finally
		{
			await fixture.DisposeAsync();
		}
	}

	// Measures the allocation of a single far viewport jump (horizontal start -> end) after a warmup
	// round-trip, so the measured jump reuses recycled containers — the steady-state the app hits when
	// it auto-scrolls to a step added while the viewport is far from the insertion point.
	private static async Task<JumpSample> MeasureViewportJumpAsync(ProbeScenario scenario, int seed)
	{
		var fixture = new UIFixture();
		await fixture.InitializeAsync(scenario.ConfigName);
		try
		{
			fixture.SeedRecipe(seed);
			var surface = fixture.CreateTransposedSurface();
			surface.Initialize();
			var view = new TransposedRecipeGridView { DataContext = surface };
			var window = new Window { Width = scenario.WindowWidth, Height = 800, Content = view };
			window.Show();
			Dispatcher.UIThread.RunJobs();

			var listBox = view.FindControl<ListBox>("StepListBox")!;
			var lastIndex = surface.StepColumns.Count - 1;

			// Warm the jump path with one full round-trip so the measured jump exercises container
			// recycling, not first-time realization.
			listBox.ScrollIntoView(lastIndex);
			Dispatcher.UIThread.RunJobs();
			ScrollListBoxToStart(listBox);
			Dispatcher.UIThread.RunJobs();

			var bytesBefore = GC.GetAllocatedBytesForCurrentThread();
			listBox.ScrollIntoView(lastIndex);
			Dispatcher.UIThread.RunJobs();
			var bytes = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;

			var realizedColumns = listBox.GetRealizedContainers().Count();

			window.Close();

			return new JumpSample(bytes, realizedColumns);
		}
		finally
		{
			await fixture.DisposeAsync();
		}
	}

	// Appends one step and scrolls the new column/row into view so the realization/layout pass for it
	// is included in the measurement, mirroring the app auto-scrolling to a freshly added step.
	private static void AppendAndRealize(UIFixture fixture, Action realize)
	{
		fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		Dispatcher.UIThread.RunJobs();
		realize();
		Dispatcher.UIThread.RunJobs();
	}

	private static void ScrollListBoxToEnd(TransposedRecipeGridView view, int count)
	{
		var listBox = view.FindControl<ListBox>("StepListBox");
		if (listBox is null || count == 0)
		{
			return;
		}

		listBox.ScrollIntoView(count - 1);

		var scrollViewer = listBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
		if (scrollViewer is not null)
		{
			scrollViewer.Offset = new Vector(scrollViewer.Extent.Width, 0);
		}
	}

	private static void ScrollListBoxToStart(ListBox listBox)
	{
		var scrollViewer = listBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
		if (scrollViewer is not null)
		{
			scrollViewer.Offset = new Vector(0, 0);
		}
	}

	private static void ScrollToEnd(Control view)
	{
		var scrollViewer = view.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
		if (scrollViewer is not null)
		{
			scrollViewer.Offset = new Vector(0, scrollViewer.Extent.Height);
		}
	}

	private readonly record struct Sample(long BytesPerAdd, double Gen0PerAdd);

	private readonly record struct JumpSample(long TotalBytes, int RealizedColumns);

	private sealed record ProbeScenario(string Label, string ConfigName, int WindowWidth);
}
