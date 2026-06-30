using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

using FluentAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Unit")]
public sealed class GridFactoryFontTests : IAsyncLifetime
{
	private const string ConfiguredFamily = "Courier New";
	private const int ConfiguredHeaderWeight = 900;
	private const int ConfiguredCellWeight = 600;
	private const int ConfiguredHeaderSize = 20;
	private const int ConfiguredCellSize = 17;

	private readonly UIFixture _fixture = new();

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
	}

	public ValueTask DisposeAsync()
	{
		return _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void HeaderTemplate_CarriesConfiguredHeaderFont()
	{
		var grid = BuildGrid(NonDefaultStyle());

		var header = (TextBlock)grid.Columns[0].HeaderTemplate!.Build(null)!;

		header.FontSize.Should().Be(ConfiguredHeaderSize);
		header.FontWeight.Should().Be((FontWeight)ConfiguredHeaderWeight);
		header.FontStyle.Should().Be(FontStyle.Italic);
		header.FontFamily.Name.Should().Be(ConfiguredFamily);
	}

	[AvaloniaFact]
	public void NumberingColumn_CarriesConfiguredCellFont()
	{
		var grid = BuildGrid(NonDefaultStyle());

		var numberingColumn = (DataGridTextColumn)grid.Columns[0];

		numberingColumn.FontSize.Should().Be(ConfiguredCellSize);
		numberingColumn.FontWeight.Should().Be((FontWeight)ConfiguredCellWeight);
		numberingColumn.FontStyle.Should().Be(FontStyle.Italic);
		numberingColumn.FontFamily!.Name.Should().Be(ConfiguredFamily);
	}

	[AvaloniaFact]
	public void NumberingColumn_EmptyFamily_UsesChromeFontDefault()
	{
		var grid = BuildGrid(GridStyleOptions.Default with { FontFamily = "" });

		var numberingColumn = (DataGridTextColumn)grid.Columns[0];

		numberingColumn.FontFamily.Should().Be(GridFonts.DefaultFamily,
			"an empty family must fall back to the chrome font for the numbering column");
	}

	[AvaloniaFact]
	public void TextCell_CarriesConfiguredCellFont()
	{
		var factory = new TextCellFactory(NonDefaultStyle());
		var columnDef = _fixture.RecipeMetadataRegistry.GetColumn("task").Value;

		var column = (DataGridTemplateColumn)factory.CreateEditableColumn(columnDef, new DataGridLength(100), maxLength: null);

		var displayCell = (TextBlock)column.CellTemplate!.Build(null)!;
		displayCell.FontSize.Should().Be(ConfiguredCellSize);
		displayCell.FontWeight.Should().Be((FontWeight)ConfiguredCellWeight);
		displayCell.FontStyle.Should().Be(FontStyle.Italic);
		displayCell.FontFamily.Name.Should().Be(ConfiguredFamily);

		var editingCell = (TextBox)column.CellEditingTemplate!.Build(null)!;
		editingCell.FontSize.Should().Be(ConfiguredCellSize);
		editingCell.FontWeight.Should().Be((FontWeight)ConfiguredCellWeight);
		editingCell.FontStyle.Should().Be(FontStyle.Italic);
		editingCell.FontFamily.Name.Should().Be(ConfiguredFamily);
	}

	[AvaloniaFact]
	public void ComboBoxCell_CarriesConfiguredCellFont()
	{
		var factory = new ComboBoxCellFactory(_fixture.RecipeMetadataRegistry, NonDefaultStyle());
		var columnDef = _fixture.RecipeMetadataRegistry.GetColumn("action").Value;

		var column = (DataGridTemplateColumn)factory.CreateActionColumn(columnDef, new DataGridLength(100));

		var comboBox = (ComboBox)column.CellTemplate!.Build(null)!;
		comboBox.FontSize.Should().Be(ConfiguredCellSize);
		comboBox.FontWeight.Should().Be((FontWeight)ConfiguredCellWeight);
		comboBox.FontStyle.Should().Be(FontStyle.Italic);
		comboBox.FontFamily.Name.Should().Be(ConfiguredFamily);
	}

	[AvaloniaFact]
	public void EmptyFamily_UsesChromeFontDefault()
	{
		var factory = new TextCellFactory(GridStyleOptions.Default with { FontFamily = "" });
		var columnDef = _fixture.RecipeMetadataRegistry.GetColumn("task").Value;

		var column = (DataGridTemplateColumn)factory.CreateEditableColumn(columnDef, new DataGridLength(100), maxLength: null);

		var displayCell = (TextBlock)column.CellTemplate!.Build(null)!;

		displayCell.FontFamily.Should().Be(GridFonts.DefaultFamily,
			"an empty family must fall back to the chrome font for grid cells");
	}

	private GridStyleOptions NonDefaultStyle()
	{
		return GridStyleOptions.Default with
		{
			FontFamily = ConfiguredFamily,
			HeaderFontSize = ConfiguredHeaderSize,
			HeaderFontWeight = ConfiguredHeaderWeight,
			HeaderItalic = true,
			CellFontSize = ConfiguredCellSize,
			CellFontWeight = ConfiguredCellWeight,
			CellItalic = true,
		};
	}

	private DataGrid BuildGrid(GridStyleOptions gridStyle)
	{
		var columnBuilder = new ColumnBuilder(gridStyle, _fixture.RecipeMetadataRegistry);
		var grid = new DataGrid();
		columnBuilder.BuildColumns(grid);

		return grid;
	}
}
