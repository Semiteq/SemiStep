using Avalonia.Controls;
using Avalonia.Data;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

public sealed class ColumnBuilder(
	GridStyleOptions gridStyle,
	RecipeMetadataRegistry recipeMetadataRegistry)
{
	private readonly ComboBoxCellFactory _comboBoxCellFactory = new(recipeMetadataRegistry);

	private readonly TextCellFactory _textCellFactory = new();
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

	private static void AddNumberingColumn(DataGrid grid)
	{
		grid.Columns.Add(new DataGridTextColumn
		{
			Header = "No",
			Binding = new Binding("StepNumber"),
			IsReadOnly = true,
			Width = DataGridLength.Auto,
			CanUserSort = false
		});
	}

	private DataGridColumn CreateColumn(GridColumnDefinition columnDef)
	{
		var width = _widthCalculator.CalculateColumnWidth(columnDef);
		var column = CreateColumnInner(columnDef, width);
		column.CellTheme = InapplicableCellTheme.Create(columnDef.Key);

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
		var isStringTyped = string.Equals(propertyDef.SystemType, "string", StringComparison.OrdinalIgnoreCase);

		return isStringTyped ? recipeMetadataRegistry.GetStringMaxLength() : null;
	}
}
