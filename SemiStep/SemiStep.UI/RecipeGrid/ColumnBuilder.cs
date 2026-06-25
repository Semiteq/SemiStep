using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

public sealed class ColumnBuilder(
	GridStyleOptions gridStyle,
	RecipeMetadataRegistry recipeMetadataRegistry)
{
	private const string ReadOnlyColumnClass = "read-only-column";
	private const string StepNumberColumnClass = "step-number-column";

	// Assigned as the column's HeaderTemplate, not as a DataGridColumnHeader ContentTemplate setter:
	// the header binds ContentTemplate from HeaderTemplate, so a style setter is overridden and the
	// header would not wrap. See Docs/architecture/recipe-grid-column-sizing.md.
	private readonly IDataTemplate _wrappingHeaderTemplate = BuildWrappingHeaderTemplate(gridStyle);

	private readonly ComboBoxCellFactory _comboBoxCellFactory = new(recipeMetadataRegistry, gridStyle);

	private readonly TextCellFactory _textCellFactory = new(gridStyle);
	private readonly ColumnWidthCalculator _widthCalculator = new(recipeMetadataRegistry, gridStyle);

	public void BuildColumns(DataGrid grid)
	{
		grid.Columns.Clear();
		AddNumberingColumn(grid);

		foreach (var columnDef in recipeMetadataRegistry.GetAllColumns())
		{
			var column = CreateColumn(columnDef);
			grid.Columns.Add(column);
		}
	}

	private void AddNumberingColumn(DataGrid grid)
	{
		var column = new DataGridTextColumn
		{
			Header = "No",
			HeaderTemplate = _wrappingHeaderTemplate,
			Binding = new Binding("StepNumber"),
			FontSize = gridStyle.CellFontSize,
			IsReadOnly = true,
			Width = DataGridLength.Auto,
			MinWidth = _widthCalculator.MinColumnWidth,
			CanUserSort = false
		};
		column.CellStyleClasses.Add(StepNumberColumnClass);
		grid.Columns.Add(column);
	}

	private static IDataTemplate BuildWrappingHeaderTemplate(GridStyleOptions gridStyle)
	{
		return new FuncDataTemplate<object?>(
			(_, _) =>
			{
				var textBlock = new TextBlock
				{
					FontSize = gridStyle.HeaderFontSize,
					FontWeight = FontWeight.Bold,
					TextWrapping = TextWrapping.Wrap,
					MaxLines = 2,
					TextTrimming = TextTrimming.CharacterEllipsis,
					TextAlignment = TextAlignment.Center,
					HorizontalAlignment = HorizontalAlignment.Stretch,
					VerticalAlignment = VerticalAlignment.Center
				};
				textBlock.Bind(TextBlock.TextProperty, new Binding());
				return textBlock;
			},
			supportsRecycling: true);
	}

	private DataGridColumn CreateColumn(GridColumnDefinition columnDef)
	{
		var width = _widthCalculator.CalculateColumnWidth(columnDef);
		var column = CreateColumnInner(columnDef, width);
		if (columnDef.ReadOnly)
		{
			column.CellStyleClasses.Add(ReadOnlyColumnClass);
		}
		column.CellTheme = InapplicableCellTheme.Create(columnDef.Key);
		// Pin absolute columns to their content width so a narrow window scrolls instead of clipping
		// (the DataGrid otherwise shrinks columns to MinWidth). See Docs/architecture/recipe-grid-column-sizing.md.
		column.MinWidth = width.IsAbsolute ? width.Value : _widthCalculator.MinColumnWidth;
		column.HeaderTemplate = _wrappingHeaderTemplate;

		return column;
	}

	private DataGridColumn CreateColumnInner(GridColumnDefinition columnDef, DataGridLength width)
	{
		if (columnDef.ColumnType == ColumnTypes.ActionComboBox)
		{
			return _comboBoxCellFactory.CreateActionColumn(columnDef, width);
		}

		if (ColumnTypes.IsGroupBoundColumn(columnDef.ColumnType))
		{
			return _comboBoxCellFactory.CreateGroupComboBoxColumn(columnDef, width);
		}

		if (columnDef.ReadOnly)
		{
			return _textCellFactory.CreateReadOnlyColumn(columnDef, width);
		}

		var maxLength = ResolveMaxLength(columnDef);
		return _textCellFactory.CreateEditableColumn(columnDef, width, maxLength);
	}

	private int? ResolveMaxLength(GridColumnDefinition columnDef)
	{
		var propertyDef = recipeMetadataRegistry.GetProperty(columnDef.PropertyTypeId).Value;
		var isStringTyped = SystemTypes.Comparer.Equals(propertyDef.SystemType, SystemTypes.String);

		return isStringTyped ? recipeMetadataRegistry.GetStringMaxLength() : null;
	}
}
