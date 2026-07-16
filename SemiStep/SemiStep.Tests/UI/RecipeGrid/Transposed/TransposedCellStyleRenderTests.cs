using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentAssertions;

using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid.Transposed;
using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

// Renders the real transposed grid to confirm the flattened background MultiBinding drives the live
// Border.Background, and that the read-only / inapplicable / selected FOREGROUND style setters kept in
// TransposedGridStyles.axaml still resolve after the background rules were removed.
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class TransposedCellStyleRenderTests : IAsyncLifetime
{
	private const int SeededStepCount = 3;
	private const string CellClass = "transposed-cell";

	private readonly UIFixture _fixture = new();
	private TransposedRecipeGridSurface _surface = null!;
	private Window? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.SeedRecipe(SeededStepCount);

		_surface = _fixture.CreateTransposedSurface();
		_surface.Initialize();
	}

	public async ValueTask DisposeAsync()
	{
		_window?.Close();
		_surface.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void EditableCell_UsesGridBackground()
	{
		var (_, stepListBox) = ShowView();
		var cells = FindCellBorders(stepListBox, 0);
		var index = IndexOfDescriptor(descriptor =>
			!descriptor.IsReadOnlyParameter && _surface.StepColumns[0].Row.IsApplicable(descriptor.ParameterKey));

		cells[index].Background.Should().BeSameAs(Resource(CellPaletteInstaller.GridBackgroundBrushKey));
	}

	[AvaloniaFact]
	public void InapplicableCell_UsesDisabledBackgroundAndForeground()
	{
		var (_, stepListBox) = ShowView();
		var row = _surface.StepColumns[0].Row;
		var cells = FindCellBorders(stepListBox, 0);
		var index = IndexOfDescriptor(descriptor =>
			!descriptor.IsReadOnlyParameter && !row.IsApplicable(descriptor.ParameterKey));

		cells[index].Background.Should().BeSameAs(Resource(CellPaletteInstaller.CellDisabledDepth0BrushKey));
		cells[index].GetValue(TextElement.ForegroundProperty)
			.Should().BeSameAs(Resource(CellPaletteInstaller.CellDisabledForegroundBrushKey));
	}

	[AvaloniaFact]
	public void ChangedCell_UsesChangedBackground()
	{
		var (_, stepListBox) = ShowView();
		var row = _surface.StepColumns[0].Row;
		var cells = FindCellBorders(stepListBox, 0);
		var index = IndexOfDescriptor(descriptor =>
			!descriptor.IsReadOnlyParameter && row.IsApplicable(descriptor.ParameterKey));

		row.MarkChanged([_surface.ParameterDescriptors[index].ParameterKey]);
		Dispatcher.UIThread.RunJobs();

		cells[index].Background.Should().BeSameAs(Resource(CellPaletteInstaller.CellChangedBrushKey));
	}

	[AvaloniaFact]
	public void Depth1EditableCell_UsesDepth1Background()
	{
		var (_, stepListBox) = ShowView();
		var row = _surface.StepColumns[0].Row;
		var index = IndexOfDescriptor(descriptor =>
			!descriptor.IsReadOnlyParameter && row.IsApplicable(descriptor.ParameterKey));

		row.ForDepth = 1;
		Dispatcher.UIThread.RunJobs();

		var cells = FindCellBorders(stepListBox, 0);
		cells[index].Background.Should().BeSameAs(Resource(ExecutionPaletteInstaller.ExecRowDepth1BrushKey));
	}

	[AvaloniaFact]
	public void ChangedAndSelectedCell_UsesChangedSelectedBackground()
	{
		var (_, stepListBox) = ShowView();
		var row = _surface.StepColumns[0].Row;
		var index = IndexOfDescriptor(descriptor =>
			!descriptor.IsReadOnlyParameter && row.IsApplicable(descriptor.ParameterKey));

		row.MarkChanged([_surface.ParameterDescriptors[index].ParameterKey]);
		_surface.RequestSelection(0);
		Dispatcher.UIThread.RunJobs();

		var cells = FindCellBorders(stepListBox, 0);
		cells[index].Background.Should().BeSameAs(
			Resource(CellPaletteInstaller.CellChangedSelectedBackgroundBrushKey),
			"changed outranks the plain selection tint when a cell is both changed and selected");
	}

	[AvaloniaFact]
	public void SelectedColumn_EditableCell_UsesSelectionBackgroundAndForeground()
	{
		var (_, stepListBox) = ShowView();
		var row = _surface.StepColumns[0].Row;
		var index = IndexOfDescriptor(descriptor =>
			!descriptor.IsReadOnlyParameter && row.IsApplicable(descriptor.ParameterKey));

		_surface.RequestSelection(0);
		Dispatcher.UIThread.RunJobs();

		var cells = FindCellBorders(stepListBox, 0);
		cells[index].Background.Should().BeSameAs(Resource(CellPaletteInstaller.SelectionBackgroundBrushKey));
		cells[index].GetValue(TextElement.ForegroundProperty)
			.Should().BeSameAs(Resource(CellPaletteInstaller.SelectionForegroundBrushKey));
	}

	[AvaloniaFact]
	public void SelectedColumn_InapplicableCell_UsesSelectedDisabledBackground_AndSelectionForeground()
	{
		var (_, stepListBox) = ShowView();
		var row = _surface.StepColumns[0].Row;
		var index = IndexOfDescriptor(descriptor =>
			!descriptor.IsReadOnlyParameter && !row.IsApplicable(descriptor.ParameterKey));

		_surface.RequestSelection(0);
		Dispatcher.UIThread.RunJobs();

		var cells = FindCellBorders(stepListBox, 0);
		cells[index].Background.Should().BeSameAs(Resource(CellPaletteInstaller.CellDisabledSelectedBackgroundBrushKey));
		cells[index].GetValue(TextElement.ForegroundProperty)
			.Should().BeSameAs(Resource(CellPaletteInstaller.SelectionForegroundBrushKey));
	}

	// A config-driven read-only column (comment, applicable under the seeded Wait action) proves the
	// static read-only-cell class and the per-slot ConverterParameter still drive the read-only palette
	// after the two reactive IsReadOnlyParameter legs were removed from BuildCellSlot; a normal applicable
	// cell in the same render must not pick up the class.
	[AvaloniaFact]
	public async Task ReadOnlyCell_CarriesReadOnlyClass_AndUsesReadOnlyBackground()
	{
		var readOnlyFixture = new UIFixture();
		await readOnlyFixture.InitializeAsync("WithReadOnlyColumn");
		try
		{
			readOnlyFixture.SeedRecipe(SeededStepCount);
			var surface = readOnlyFixture.CreateTransposedSurface();
			surface.Initialize();

			var view = new TransposedRecipeGridView { DataContext = surface };
			var window = new Window
			{
				Width = 1200,
				Height = 800,
				Content = view,
			};
			CellPaletteInstaller.Install(window.Resources, readOnlyFixture.AppConfiguration.GridStyle);
			ExecutionPaletteInstaller.Install(window.Resources, readOnlyFixture.AppConfiguration.GridStyle);

			var stepListBox = view.FindControl<ListBox>("StepListBox");
			stepListBox.Should().NotBeNull();
			// Exercise the recycle-in-place panel (the production template swap lands in Task 5).
			stepListBox!.UseTransposedColumnsPanel();

			window.Show();
			Dispatcher.UIThread.RunJobs();

			var row = surface.StepColumns[0].Row;
			var descriptors = surface.ParameterDescriptors;
			var readOnlyIndex = IndexOfDescriptorIn(
				descriptors,
				descriptor => descriptor.IsReadOnlyParameter && row.IsApplicable(descriptor.ParameterKey));
			var normalIndex = IndexOfDescriptorIn(
				descriptors,
				descriptor => !descriptor.IsReadOnlyParameter && row.IsApplicable(descriptor.ParameterKey));

			var cells = FindCellBorders(stepListBox!, 0);

			cells[readOnlyIndex].Classes.Should().Contain(
				"read-only-cell", "the read-only descriptor still tags its slot statically");
			cells[readOnlyIndex].Background.Should().BeSameAs(
				ResourceFrom(window, CellPaletteInstaller.CellReadOnlyDepth0BrushKey),
				"the ConverterParameter feeds the read-only palette in place of the removed bound leg");
			cells[normalIndex].Classes.Should().NotContain(
				"read-only-cell", "a non-read-only descriptor must not carry the read-only class");

			window.Close();
			surface.Dispose();
		}
		finally
		{
			await readOnlyFixture.DisposeAsync();
		}
	}

	private static int IndexOfDescriptorIn(
		IReadOnlyList<ParameterDescriptor> descriptors, Func<ParameterDescriptor, bool> predicate)
	{
		for (var i = 0; i < descriptors.Count; i++)
		{
			if (predicate(descriptors[i]))
			{
				return i;
			}
		}

		throw new InvalidOperationException("No parameter descriptor matches the test predicate.");
	}

	private static IBrush ResourceFrom(Window window, string key)
	{
		window.TryFindResource(key, out var value).Should().BeTrue($"resource '{key}' must be installed");
		return value.Should().BeAssignableTo<IBrush>().Subject;
	}

	private IBrush Resource(string key)
	{
		return ResourceFrom(_window!, key);
	}

	private int IndexOfDescriptor(Func<ParameterDescriptor, bool> predicate)
	{
		return IndexOfDescriptorIn(_surface.ParameterDescriptors, predicate);
	}

	private static List<Border> FindCellBorders(ListBox stepListBox, int columnIndex)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!;
		return container.GetVisualDescendants()
			.OfType<Border>()
			.Where(border => border.Classes.Contains(CellClass))
			.ToList();
	}

	private (TransposedRecipeGridView View, ListBox StepListBox) ShowView()
	{
		var view = new TransposedRecipeGridView { DataContext = _surface };
		_window = new Window
		{
			Width = 1200,
			Height = 800,
			Content = view,
		};

		CellPaletteInstaller.Install(_window.Resources, _fixture.AppConfiguration.GridStyle);
		ExecutionPaletteInstaller.Install(_window.Resources, _fixture.AppConfiguration.GridStyle);

		var stepListBox = view.FindControl<ListBox>("StepListBox");
		stepListBox.Should().NotBeNull();
		// Exercise the recycle-in-place panel (the production template swap lands in Task 5).
		stepListBox!.UseTransposedColumnsPanel();

		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return (view, stepListBox);
	}
}
