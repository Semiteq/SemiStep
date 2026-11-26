using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Tests.UI.Helpers;
using SemiStep.Tests.UI.RecipeGrid.Transposed;

using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;
using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

// The focused cell's 2 px accent frame in both grids. The canonical frame is Semi's current-cell
// rectangle, a theme-private part, so a Semi update that renames or restyles it fails here.
[Trait("Component", "UI")]
[Trait("Area", "FocusFrame")]
[Trait("Category", "Integration")]
public sealed class FocusFrameTests : IAsyncLifetime
{
	private static readonly Color _accent = Color.Parse("#3574F0");

	private readonly UIFixture _fixture = new();
	private Window? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync("WithReadOnlyColumn");
		_fixture.SeedRecipe(3);
	}

	public async ValueTask DisposeAsync()
	{
		_window?.Close();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void CanonicalCurrentCell_DrawsA2PxAccentRectangle()
	{
		var surface = _fixture.CreateCanonicalSurface();
		surface.Initialize();
		var view = new CanonicalRecipeGridView { DataContext = surface };
		Show(view);
		var dataGrid = view.FindControl<DataGrid>("RecipeGrid")!;
		var column = dataGrid.Columns.Single(candidate => Equals(candidate.Tag, EditableKey(surface.RecipeRows[0])));

		dataGrid.Focus();
		dataGrid.SelectedIndex = 0;
		dataGrid.CurrentColumn = column;
		Dispatcher.UIThread.RunJobs();

		var cell = column.GetCellContent(surface.RecipeRows[0])!.FindAncestorOfType<DataGridCell>()!;
		cell.Classes.Should().Contain([":current", ":focus"], "both Semi rectangles must be on for the check to mean anything");

		StrokeColors(cell).Should().OnlyContain(color => color == _accent).And.NotBeEmpty();
		RenderedFrame(cell).Should().Be(new Thickness(2), "the stacked rectangles draw a 2 px frame on every side");
	}

	[AvaloniaFact]
	public void CanonicalCurrentCell_FollowsTheConfiguredAccent_AndKeepsItsSize()
	{
		var gridStyle = _fixture.AppConfiguration.GridStyle;
		var custom = gridStyle with { Selection = gridStyle.Selection with { Accent = StyleColor.Parse("#C0392B") } };
		var surface = _fixture.CreateCanonicalSurface();
		surface.Initialize();
		var view = new CanonicalRecipeGridView { DataContext = surface };
		Show(view, custom);
		var dataGrid = view.FindControl<DataGrid>("RecipeGrid")!;
		var column = dataGrid.Columns.Single(candidate => Equals(candidate.Tag, EditableKey(surface.RecipeRows[0])));
		var cell = column.GetCellContent(surface.RecipeRows[0])!.FindAncestorOfType<DataGridCell>()!;
		var sizeBefore = cell.Bounds.Size;

		dataGrid.Focus();
		dataGrid.SelectedIndex = 0;
		dataGrid.CurrentColumn = column;
		Dispatcher.UIThread.RunJobs();

		StrokeColors(cell).Should().OnlyContain(color => color == Color.Parse("#C0392B"),
			"the frame takes selection.accent from the grid style").And.NotBeEmpty();
		RenderedFrame(cell).Should().Be(new Thickness(2));
		cell.Bounds.Size.Should().Be(sizeBefore, "the frame draws inside the cell and moves no layout");
	}

	[AvaloniaFact]
	public void TransposedFocusedEditableCell_ShowsTheFrame_AndKeepsItsSize()
	{
		var (surface, stepListBox) = ShowTransposed();
		var row = surface.StepColumns[0].Row;
		var slot = SlotOf(surface, EditableKey(row));
		var cell = CellBorder(stepListBox, 0, slot);
		var presenter = cell.GetVisualDescendants().OfType<LazyCellPresenter>().First();
		var sizeBefore = cell.Bounds.Size;

		presenter.Focus().Should().BeTrue();
		Dispatcher.UIThread.RunJobs();

		presenter.BoxShadow.Count.Should().Be(1);
		var shadow = presenter.BoxShadow[0];
		shadow.IsInset.Should().BeTrue();
		shadow.Spread.Should().Be(2);
		shadow.Blur.Should().Be(0);
		shadow.Color.Should().Be(_accent);
		cell.Bounds.Size.Should().Be(sizeBefore, "the frame draws inside the cell and moves no layout");
	}

	[AvaloniaFact]
	public void TransposedReadOnlyCell_TakesNoFocus_AndShowsNoFrame()
	{
		var (surface, stepListBox) = ShowTransposed();
		var row = surface.StepColumns[0].Row;
		var cell = CellBorder(stepListBox, 0, SlotOf(surface, ReadOnlyKey(row)));

		cell.GetVisualDescendants().OfType<LazyCellPresenter>().Should().BeEmpty();
		cell.GetVisualDescendants().OfType<InputElement>().Should().OnlyContain(element => !element.Focusable);
	}

	[AvaloniaFact]
	public void TransposedUnfocusedCell_ShowsNoFrame()
	{
		var (surface, stepListBox) = ShowTransposed();
		var row = surface.StepColumns[0].Row;
		var presenter = CellBorder(stepListBox, 0, SlotOf(surface, EditableKey(row)))
			.GetVisualDescendants().OfType<LazyCellPresenter>().First();

		presenter.BoxShadow.Count.Should().Be(0);
	}

	private static List<Rectangle> StrokedRectangles(DataGridCell cell)
	{
		return [.. cell.GetVisualDescendants()
			.OfType<Rectangle>()
			.Where(rectangle => rectangle.IsEffectivelyVisible && rectangle.Stroke is not null && rectangle.StrokeThickness > 0)];
	}

	private static List<Color?> StrokeColors(DataGridCell cell)
	{
		return [.. StrokedRectangles(cell).Select(rectangle => (rectangle.Stroke as ISolidColorBrush)?.Color)];
	}

	// Per side, the span from the outermost to the innermost stroked pixel over every frame rectangle in the cell.
	private static Thickness RenderedFrame(DataGridCell cell)
	{
		var bands = StrokedRectangles(cell)
			.Select(rectangle => (
				Box: new Rect(rectangle.TranslatePoint(default, cell)!.Value, rectangle.Bounds.Size),
				Stroke: rectangle.StrokeThickness))
			.ToList();

		return new Thickness(
			Span(bands.Select(band => (band.Box.Left, band.Box.Left + band.Stroke))),
			Span(bands.Select(band => (band.Box.Top, band.Box.Top + band.Stroke))),
			Span(bands.Select(band => (band.Box.Right - band.Stroke, band.Box.Right))),
			Span(bands.Select(band => (band.Box.Bottom - band.Stroke, band.Box.Bottom))));
	}

	private static double Span(IEnumerable<(double Start, double End)> bands)
	{
		var list = bands.ToList();
		return list.Max(band => band.End) - list.Min(band => band.Start);
	}

	private static int SlotOf(TransposedRecipeGridSurface surface, string columnKey)
	{
		return surface.ParameterDescriptors
			.Select((descriptor, index) => (descriptor, index))
			.First(pair => pair.descriptor.ParameterKey == columnKey)
			.index;
	}

	private static Border CellBorder(ListBox stepListBox, int columnIndex, int slot)
	{
		var container = (ListBoxItem)stepListBox.ContainerFromIndex(columnIndex)!;
		return container.GetVisualDescendants()
			.OfType<Border>()
			.Where(border => border.Classes.Contains("transposed-cell"))
			.ElementAt(slot);
	}

	private string EditableKey(RecipeRowViewModel row)
	{
		return ColumnKey(column =>
			!column.ReadOnly && row.IsApplicable(column.Key) && !ColumnTypes.IsActionComboBoxColumn(column.ColumnType));
	}

	private string ReadOnlyKey(RecipeRowViewModel row)
	{
		return ColumnKey(column => column.ReadOnly && row.IsApplicable(column.Key));
	}

	private string ColumnKey(Func<GridColumnDefinition, bool> predicate)
	{
		return _fixture.RecipeMetadataRegistry.GetAllColumns().First(predicate).Key;
	}

	private (TransposedRecipeGridSurface Surface, ListBox StepListBox) ShowTransposed()
	{
		var surface = _fixture.CreateTransposedSurface();
		surface.Initialize();
		var view = new TransposedRecipeGridView { DataContext = surface };
		var stepListBox = view.FindControl<ListBox>("StepListBox")!;
		stepListBox.UseTransposedColumnsPanel();
		Show(view);

		return (surface, stepListBox);
	}

	private void Show(Control view, GridStyleOptions? gridStyle = null)
	{
		_window = new Window { Width = 1600, Height = 600 };
		CellPaletteInstaller.Install(_window.Resources, gridStyle ?? _fixture.AppConfiguration.GridStyle);
		_window.Content = view;
		_window.Show();
		Dispatcher.UIThread.RunJobs();
	}
}
