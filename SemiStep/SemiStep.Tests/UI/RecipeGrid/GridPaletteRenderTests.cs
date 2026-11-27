using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Tests.UI.Helpers;
using SemiStep.Tests.UI.RecipeGrid.Transposed;

using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;
using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

// Reads the realised cell background of both grids back as ISolidColorBrush and compares it with the
// approved table (docs/architecture/grid-style-configuration.md, "Cell palette").
[Trait("Component", "UI")]
[Trait("Area", "GridPalette")]
[Trait("Category", "Integration")]
public sealed class GridPaletteRenderTests : IAsyncLifetime
{
	private const int SeededStepCount = 60;

	private readonly UIFixture _fixture = new();
	private Window? _window;
	private CanonicalRecipeGridSurface? _canonical;
	private TransposedRecipeGridSurface? _transposed;
	private DataGrid? _dataGrid;
	private ListBox? _stepListBox;

	public enum GridKind
	{
		Canonical,
		Transposed,
	}

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync("WithReadOnlyColumn");
		_fixture.SeedRecipe(SeededStepCount);
	}

	public async ValueTask DisposeAsync()
	{
		_window?.Close();
		await _fixture.DisposeAsync();
	}

	[AvaloniaTheory]
	[InlineData(GridKind.Canonical, "idle", "#FFFFFF")]
	[InlineData(GridKind.Canonical, "past", "#E4F1E8")]
	[InlineData(GridKind.Canonical, "running", "#BCDCC5")]
	[InlineData(GridKind.Canonical, "depth1", "#F5F1FB")]
	[InlineData(GridKind.Canonical, "depth2", "#EAE3F6")]
	[InlineData(GridKind.Canonical, "depth3", "#DDD1F1")]
	[InlineData(GridKind.Canonical, "selected", "#CDDCFB")]
	[InlineData(GridKind.Canonical, "selected-running", "#9AC2D0")]
	[InlineData(GridKind.Transposed, "idle", "#FFFFFF")]
	[InlineData(GridKind.Transposed, "past", "#E4F1E8")]
	[InlineData(GridKind.Transposed, "running", "#BCDCC5")]
	[InlineData(GridKind.Transposed, "depth1", "#F5F1FB")]
	[InlineData(GridKind.Transposed, "depth2", "#EAE3F6")]
	[InlineData(GridKind.Transposed, "depth3", "#DDD1F1")]
	[InlineData(GridKind.Transposed, "selected", "#CDDCFB")]
	[InlineData(GridKind.Transposed, "selected-running", "#9AC2D0")]
	public void PlainCell_ShowsTheTableColour(GridKind grid, string state, string expected)
	{
		Show(grid);
		Apply(Row(0), state);

		HexOf(Background(grid, 0, EditableKey())).Should().Be(expected);
	}

	[AvaloniaTheory]
	[InlineData(GridKind.Canonical, "idle", "#D8D8D8")]
	[InlineData(GridKind.Canonical, "past", "#C2CFC5")]
	[InlineData(GridKind.Canonical, "running", "#A1C1A9")]
	[InlineData(GridKind.Canonical, "depth3", "#C0B4D4")]
	[InlineData(GridKind.Canonical, "selected", "#AFBFDE")]
	[InlineData(GridKind.Transposed, "idle", "#D8D8D8")]
	[InlineData(GridKind.Transposed, "past", "#C2CFC5")]
	[InlineData(GridKind.Transposed, "running", "#A1C1A9")]
	[InlineData(GridKind.Transposed, "depth3", "#C0B4D4")]
	[InlineData(GridKind.Transposed, "selected", "#AFBFDE")]
	public void ReadOnlyCell_ShowsTheTableColour(GridKind grid, string state, string expected)
	{
		Show(grid);
		Apply(Row(0), state);

		HexOf(Background(grid, 0, ReadOnlyKey())).Should().Be(expected);
	}

	[AvaloniaTheory]
	[InlineData(GridKind.Canonical, "idle", "#E0E0E0")]
	[InlineData(GridKind.Canonical, "past", "#C9D6CC")]
	[InlineData(GridKind.Canonical, "running", "#A6C6AF")]
	[InlineData(GridKind.Canonical, "depth2", "#D0C8DC")]
	[InlineData(GridKind.Canonical, "selected", "#B5C5E4")]
	[InlineData(GridKind.Transposed, "idle", "#E0E0E0")]
	[InlineData(GridKind.Transposed, "past", "#C9D6CC")]
	[InlineData(GridKind.Transposed, "running", "#A6C6AF")]
	[InlineData(GridKind.Transposed, "depth2", "#D0C8DC")]
	[InlineData(GridKind.Transposed, "selected", "#B5C5E4")]
	public void InapplicableCell_ShowsTheTableColour(GridKind grid, string state, string expected)
	{
		Show(grid);
		Apply(Row(0), state);

		HexOf(Background(grid, 0, InapplicableKey())).Should().Be(expected);
	}

	[AvaloniaTheory]
	[InlineData(GridKind.Canonical, false, "#FFCC80")]
	[InlineData(GridKind.Canonical, true, "#CDB69C")]
	[InlineData(GridKind.Transposed, false, "#FFCC80")]
	[InlineData(GridKind.Transposed, true, "#CDB69C")]
	public void MarkedCellInAReadOnlyColumn_ShowsOrange(GridKind grid, bool selected, string expected)
	{
		Show(grid);
		Row(0).MarkChanged([ReadOnlyKey()]);
		Apply(Row(0), selected ? "selected" : "idle");

		HexOf(Background(grid, 0, ReadOnlyKey())).Should().Be(expected);
	}

	[AvaloniaTheory]
	[InlineData(GridKind.Canonical)]
	[InlineData(GridKind.Transposed)]
	public void ReadOnlyInapplicableAndSelectedCellText_IsBlack(GridKind grid)
	{
		Show(grid);
		Apply(Row(0), "selected");

		HexOf(Foreground(grid, 0, ReadOnlyKey())).Should().Be("#000000");
		HexOf(Foreground(grid, 0, InapplicableKey())).Should().Be("#000000");
		HexOf(Foreground(grid, 0, EditableKey())).Should().Be("#000000");
		HexOf(Foreground(grid, 1, ReadOnlyKey())).Should().Be("#000000");
		HexOf(Foreground(grid, 1, InapplicableKey())).Should().Be("#000000");
	}

	// The grid line stays darker than both idle greys, so grey cells keep visible edges (#157).
	[AvaloniaTheory]
	[InlineData(GridKind.Canonical)]
	[InlineData(GridKind.Transposed)]
	public void GridLine_IsTheApprovedGrey(GridKind grid)
	{
		Show(grid);

		if (grid == GridKind.Canonical)
		{
			HexOf(_dataGrid!.HorizontalGridLinesBrush).Should().Be("#B8B8B8");
			HexOf(_dataGrid.VerticalGridLinesBrush).Should().Be("#B8B8B8");
			return;
		}

		HexOf(TransposedCell(0, InapplicableKey()).BorderBrush).Should().Be("#B8B8B8");
	}

	[AvaloniaFact]
	public void CanonicalStepNumberCell_ShowsTheRowTint()
	{
		Show(GridKind.Canonical);
		Apply(Row(0), "running");

		var row = _dataGrid!.GetVisualDescendants().OfType<DataGridRow>()
			.First(candidate => ReferenceEquals(candidate.DataContext, Row(0)));
		var numberCell = row.GetVisualDescendants().OfType<DataGridCell>().First();

		HexOf(numberCell.Background).Should().Be("#BCDCC5");
	}

	[AvaloniaFact]
	public void CanonicalCellRealisedAfterAScroll_ShowsTheTableColour()
	{
		Show(GridKind.Canonical);
		var lastRow = Row(SeededStepCount - 1);
		lastRow.IsPastStep = true;

		_dataGrid!.ScrollIntoView(lastRow, null);
		Dispatcher.UIThread.RunJobs();

		HexOf(Background(GridKind.Canonical, SeededStepCount - 1, EditableKey())).Should().Be("#E4F1E8");
	}

	[AvaloniaTheory]
	[InlineData(GridKind.Canonical)]
	[InlineData(GridKind.Transposed)]
	public void RealisedCell_LooksThePaletteUpOnce_HoweverOftenItsStateChanges(GridKind grid)
	{
		var palette = CountPaletteLookups();
		Show(grid);

		palette.Lookups = 0;
		Apply(Row(1), "past");
		Apply(Row(0), "selected-running");

		palette.Lookups.Should().Be(0, "each realised cell keeps the palette it found first");
		HexOf(Background(grid, 0, EditableKey())).Should().Be("#9AC2D0");
		HexOf(Background(grid, 1, EditableKey())).Should().Be("#E4F1E8");
	}

	private static string HexOf(IBrush? brush)
	{
		var color = brush.Should().BeAssignableTo<ISolidColorBrush>().Subject.Color;
		return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
	}

	private void Apply(RecipeRowViewModel row, string state)
	{
		switch (state)
		{
			case "past":
				row.IsPastStep = true;
				break;
			case "running":
				row.IsCurrentStep = true;
				break;
			case "depth1":
				row.ForDepth = 1;
				break;
			case "depth2":
				row.ForDepth = 2;
				break;
			case "depth3":
				row.ForDepth = 3;
				break;
			case "selected":
				Select(row);
				break;
			case "selected-running":
				row.IsCurrentStep = true;
				Select(row);
				break;
		}

		Dispatcher.UIThread.RunJobs();
	}

	private void Select(RecipeRowViewModel row)
	{
		var index = Rows().IndexOf(row);
		if (_canonical is not null)
		{
			_canonical.RequestSelection(index);
		}
		else
		{
			_transposed!.RequestSelection(index);
		}

		Dispatcher.UIThread.RunJobs();
	}

	private List<RecipeRowViewModel> Rows()
	{
		return _canonical is not null
			? [.. _canonical.RecipeRows]
			: [.. _transposed!.StepColumns.Select(column => column.Row)];
	}

	private RecipeRowViewModel Row(int index)
	{
		return Rows()[index];
	}

	private string EditableKey()
	{
		return ColumnKey(column =>
			!column.ReadOnly && Row(0).IsApplicable(column.Key) && !ColumnTypes.IsActionComboBoxColumn(column.ColumnType));
	}

	private string ReadOnlyKey()
	{
		return ColumnKey(column => column.ReadOnly && Row(0).IsApplicable(column.Key));
	}

	private string InapplicableKey()
	{
		return ColumnKey(column => !column.ReadOnly && !Row(0).IsApplicable(column.Key));
	}

	private string ColumnKey(Func<GridColumnDefinition, bool> predicate)
	{
		return _fixture.RecipeMetadataRegistry.GetAllColumns().First(predicate).Key;
	}

	private IBrush? Background(GridKind grid, int rowIndex, string columnKey)
	{
		return grid == GridKind.Canonical
			? CanonicalCell(rowIndex, columnKey).Background
			: TransposedCell(rowIndex, columnKey).Background;
	}

	private IBrush? Foreground(GridKind grid, int rowIndex, string columnKey)
	{
		Control cell = grid == GridKind.Canonical
			? CanonicalCell(rowIndex, columnKey)
			: TransposedCell(rowIndex, columnKey);

		return cell.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Foreground
			?? cell.GetValue(TextElement.ForegroundProperty);
	}

	private DataGridCell CanonicalCell(int rowIndex, string columnKey)
	{
		var column = _dataGrid!.Columns.Single(candidate => Equals(candidate.Tag, columnKey));
		var content = column.GetCellContent(Row(rowIndex))
			?? throw new InvalidOperationException($"Row {rowIndex} is not realised.");

		return content.FindAncestorOfType<DataGridCell>()
			?? throw new InvalidOperationException("Cell content has no DataGridCell ancestor.");
	}

	private Border TransposedCell(int rowIndex, string columnKey)
	{
		var slot = _transposed!.ParameterDescriptors
			.Select((descriptor, index) => (descriptor, index))
			.First(pair => pair.descriptor.ParameterKey == columnKey)
			.index;
		var container = (ListBoxItem)_stepListBox!.ContainerFromIndex(rowIndex)!;

		return container.GetVisualDescendants()
			.OfType<Border>()
			.Where(border => border.Classes.Contains("transposed-cell"))
			.ElementAt(slot);
	}

	// Moves the installed palette into a provider that counts the lookups of its key.
	private CountingPaletteProvider CountPaletteLookups()
	{
		_window = new Window { Width = 1600, Height = 600 };
		CellPaletteInstaller.Install(_window.Resources, _fixture.AppConfiguration.GridStyle);
		var provider = new CountingPaletteProvider(_window.Resources[CellPalette.ResourceKey]!);
		_window.Resources.Remove(CellPalette.ResourceKey);
		_window.Resources.MergedDictionaries.Add(provider);

		return provider;
	}

	private void Show(GridKind grid)
	{
		if (_window is null)
		{
			_window = new Window { Width = 1600, Height = 600 };
			CellPaletteInstaller.Install(_window.Resources, _fixture.AppConfiguration.GridStyle);
		}

		if (grid == GridKind.Canonical)
		{
			_canonical = _fixture.CreateCanonicalSurface();
			_canonical.Initialize();
			var view = new CanonicalRecipeGridView { DataContext = _canonical };
			_window.Content = view;
			_window.Show();
			Dispatcher.UIThread.RunJobs();
			_dataGrid = view.FindControl<DataGrid>("RecipeGrid");
			return;
		}

		_transposed = _fixture.CreateTransposedSurface();
		_transposed.Initialize();
		var transposedView = new TransposedRecipeGridView { DataContext = _transposed };
		_stepListBox = transposedView.FindControl<ListBox>("StepListBox")!;
		_stepListBox.UseTransposedColumnsPanel();
		_window.Content = transposedView;
		_window.Show();
		Dispatcher.UIThread.RunJobs();
	}

	private sealed class CountingPaletteProvider(object palette) : ResourceProvider
	{
		public int Lookups { get; set; }

		public override bool HasResources => true;

		public override bool TryGetResource(object key, ThemeVariant? theme, out object? value)
		{
			if (Equals(key, CellPalette.ResourceKey))
			{
				Lookups++;
				value = palette;
				return true;
			}

			value = null;
			return false;
		}
	}
}
