using System.Diagnostics;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Tests.UI.Helpers;
using SemiStep.Tests.UI.RecipeGrid.Transposed;
using SemiStep.UI.RecipeGrid.Transposed;
using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.Performance;

// Record-only: prints how long the press that opens a combo list takes on a newly selected row or column.
// Asserts only that the press alone opens the list; wall-clock milliseconds are never asserted.
[Trait("Category", "Performance")]
[Trait("Component", "UI")]
[Trait("Area", "ComboOpen")]
public sealed class ComboPressOpenProbe(ITestOutputHelper output)
{
	private const string ConfigName = "WithGroups";
	private const int SeedSteps = 60;
	private const int WarmUpPresses = 1;
	private const int SampledPresses = 6;

	[AvaloniaFact(Explicit = true)]
	public async Task Canonical_PressOnNewRow_OpensList()
	{
		var fixture = new UIFixture();
		await fixture.InitializeAsync(ConfigName);
		fixture.SeedRecipe(SeedSteps);
		var grid = new CanonicalGridWindow(fixture);

		for (var rowIndex = 1; rowIndex <= WarmUpPresses + SampledPresses; rowIndex++)
		{
			var comboBox = grid.ComboAt(rowIndex, "action");
			var point = grid.CenterOf(comboBox);

			var milliseconds = TimePress(grid.Window, point);

			comboBox.IsDropDownOpen.Should().BeTrue($"the press alone opens the list on row {rowIndex}");
			Record("canonical", rowIndex, milliseconds);
			grid.Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
			grid.PressKey(Key.Escape, PhysicalKey.Escape);
		}

		grid.Dispose();
		await fixture.DisposeAsync();
	}

	[AvaloniaFact(Explicit = true)]
	public async Task Transposed_PressOnNewColumn_OpensList()
	{
		var fixture = new UIFixture();
		await fixture.InitializeAsync(ConfigName);
		fixture.SeedRecipe(SeedSteps);
		var surface = fixture.CreateTransposedSurface();
		surface.Initialize();
		var view = new TransposedRecipeGridView { DataContext = surface };
		var window = new Window { Width = 1600, Height = 800, Content = view };
		CellPaletteInstaller.Install(window.Resources, fixture.AppConfiguration.GridStyle);
		var stepListBox = view.FindControl<TransposedStepListBox>("StepListBox")!;
		stepListBox.UseTransposedColumnsPanel();
		window.Show();
		Dispatcher.UIThread.RunJobs();

		for (var column = 1; column <= WarmUpPresses + SampledPresses; column++)
		{
			var container = (ListBoxItem)stepListBox.ContainerFromIndex(column)!;
			var presenter = container.GetVisualDescendants().OfType<TransposedComboCellPresenter>()
				.First(p => p.IsEffectivelyEnabled);
			var point = presenter.TranslatePoint(new Point(4, 4), window)!.Value;

			var milliseconds = TimePress(window, point);

			(presenter.Editor is ComboBox { IsDropDownOpen: true }).Should().BeTrue(
				$"the first press alone opens the list on column {column}");
			Record("transposed", column, milliseconds);
			window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
			window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
			Dispatcher.UIThread.RunJobs();
		}

		window.Close();
		surface.Dispose();
		await fixture.DisposeAsync();
	}

	private static double TimePress(Window window, Point point)
	{
		var stopwatch = Stopwatch.StartNew();
		window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		return stopwatch.Elapsed.TotalMilliseconds;
	}

	private void Record(string grid, int index, double milliseconds)
	{
		var kind = index <= WarmUpPresses ? "warm-up" : "sample";
		output.WriteLine(string.Create(
			CultureInfo.InvariantCulture,
			$"{grid} {kind} {index}: press-to-open {milliseconds:F1} ms"));
	}
}
