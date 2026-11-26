using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;

using SemiStep.UI.Localization;

namespace SemiStep.UI.RecipeGrid;

public sealed class ColumnBuilder(
	GridStyleOptions gridStyle,
	RecipeMetadataRegistry recipeMetadataRegistry,
	ComboBoxItemsProvider comboBoxItemsProvider)
{
	private const string ReadOnlyColumnClass = "read-only-column";
	private const string StepNumberColumnClass = "step-number-column";

	// Assigned as the column's HeaderTemplate, not as a DataGridColumnHeader ContentTemplate setter:
	// the header binds ContentTemplate from HeaderTemplate, so a style setter is overridden.
	// See docs/architecture/recipe-grid-column-sizing.md.
	private readonly IDataTemplate _headerTemplate = BuildHeaderTemplate(gridStyle);

	private readonly ComboBoxCellFactory _comboBoxCellFactory = new(comboBoxItemsProvider, gridStyle);

	private readonly TextCellFactory _textCellFactory = new(gridStyle);
	private readonly ColumnWidthCalculator _widthCalculator = new(recipeMetadataRegistry, gridStyle);

	private readonly IReadOnlyDictionary<string, int?> _maxLengthByColumn =
		StringColumnMaxLengths.Build(recipeMetadataRegistry);

	internal void BuildColumns(DataGrid grid, CellEditCoordinator comboEditCoordinator)
	{
		grid.RowHeight = gridStyle.Layout.RowHeight;
		// The numbering column is a DataGridTextColumn with no FontFeatures property; tabular figures
		// reach its generated cells through the inherited grid-level value set here.
		grid.SetValue(TextElement.FontFeaturesProperty, GridFonts.TabularFigures);
		grid.Columns.Clear();
		AddNumberingColumn(grid);

		foreach (var columnDef in recipeMetadataRegistry.GetAllColumns())
		{
			var column = CreateColumn(columnDef, comboEditCoordinator);
			grid.Columns.Add(column);
		}
	}

	private void AddNumberingColumn(DataGrid grid)
	{
		var column = new DataGridTextColumn
		{
			Header = Resources.StepNumberHeader,
			HeaderTemplate = _headerTemplate,
			Binding = new Binding("StepNumber"),
			FontSize = gridStyle.Fonts.CellFontSize,
			FontWeight = (FontWeight)gridStyle.Fonts.CellFontWeight,
			FontStyle = gridStyle.Fonts.CellItalic ? FontStyle.Italic : FontStyle.Normal,
			IsReadOnly = true,
			Width = DataGridLength.Auto,
			MinWidth = _widthCalculator.MinColumnWidth,
			CanUserSort = false,
			FontFamily = string.IsNullOrWhiteSpace(gridStyle.Fonts.FontFamily)
				? GridFonts.DefaultFamily
				: new FontFamily(gridStyle.Fonts.FontFamily)
		};

		column.CellStyleClasses.Add(StepNumberColumnClass);
		grid.Columns.Add(column);
	}

	// One line that trims at the column edge; the tooltip always holds the full text, trimmed or not.
	private static IDataTemplate BuildHeaderTemplate(GridStyleOptions gridStyle)
	{
		return new FuncDataTemplate<object?>(
			(_, _) =>
			{
				var textBlock = new TextBlock
				{
					TextWrapping = TextWrapping.NoWrap,
					MaxLines = 1,
					TextTrimming = TextTrimming.CharacterEllipsis,
					TextAlignment = TextAlignment.Center,
					HorizontalAlignment = HorizontalAlignment.Stretch,
					VerticalAlignment = VerticalAlignment.Center
				};
				GridFontApplier.ApplyHeaderFont(textBlock, gridStyle);
				textBlock.Bind(TextBlock.TextProperty, new Binding());
				textBlock.Bind(ToolTip.TipProperty, new Binding());
				return textBlock;
			},
			supportsRecycling: true);
	}

	private DataGridColumn CreateColumn(GridColumnDefinition columnDef, CellEditCoordinator comboEditCoordinator)
	{
		var width = _widthCalculator.CalculateColumnWidth(columnDef);
		var column = CreateColumnInner(columnDef, width, comboEditCoordinator);
		if (columnDef.ReadOnly)
		{
			column.CellStyleClasses.Add(ReadOnlyColumnClass);
		}
		column.CellTheme = InapplicableCellTheme.Create(columnDef.Key);
		// Pin absolute columns to their content width so a narrow window scrolls instead of clipping
		// (the DataGrid otherwise shrinks columns to MinWidth). See docs/architecture/recipe-grid-column-sizing.md.
		column.MinWidth = width.IsAbsolute ? width.Value : _widthCalculator.MinColumnWidth;
		column.HeaderTemplate = _headerTemplate;

		return column;
	}

	private DataGridColumn CreateColumnInner(
		GridColumnDefinition columnDef,
		DataGridLength width,
		CellEditCoordinator comboEditCoordinator)
	{
		if (ColumnTypes.IsActionComboBoxColumn(columnDef.ColumnType))
		{
			return _comboBoxCellFactory.CreateActionColumn(columnDef, width, comboEditCoordinator);
		}

		if (ColumnTypes.IsGroupBoundColumn(columnDef.ColumnType))
		{
			return _comboBoxCellFactory.CreateGroupComboBoxColumn(columnDef, width, comboEditCoordinator);
		}

		var isNumeric = _widthCalculator.IsNumericColumn(columnDef);
		var alignment = new CellAlignment(isNumeric, isNumeric ? _widthCalculator.UnitSlotWidth(columnDef.Key) : 0);
		if (columnDef.ReadOnly)
		{
			return _textCellFactory.CreateReadOnlyColumn(columnDef, width, alignment);
		}

		var maxLength = _maxLengthByColumn.GetValueOrDefault(columnDef.Key);
		return _textCellFactory.CreateEditableColumn(columnDef, width, maxLength, alignment);
	}
}
