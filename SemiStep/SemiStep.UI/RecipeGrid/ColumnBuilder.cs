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

	public void BuildColumnsFromConfiguration(DataGrid grid, AppConfiguration config)
	{
		grid.Columns.Clear();
		_comboBoxCellFactory.InvalidateCaches();
		AddNumberingColumn(grid);
		AddColumnsFromConfig(grid, config);
	}

	public void BuildColumns(DataGrid grid)
	{
		grid.Columns.Clear();
		_comboBoxCellFactory.InvalidateCaches();
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

	private void AddColumnsFromConfig(DataGrid grid, AppConfiguration config)
	{
		foreach (var columnDef in config.Columns.Values)
		{
			var column = CreateColumn(columnDef);
			grid.Columns.Add(column);
		}
	}

	private DataGridColumn CreateColumn(GridColumnDefinition columnDef)
	{
		var width = _widthCalculator.CalculateColumnWidth(columnDef);

		if (columnDef.Key == ColumnTypes.Action)
		{
			return _comboBoxCellFactory.CreateActionColumn(columnDef, width);
		}

		if (IsGroupBasedComboBox(columnDef.ColumnType))
		{
			return _comboBoxCellFactory.CreateGroupComboBoxColumn(columnDef, width);
		}

		if (columnDef.ReadOnly)
		{
			return _textCellFactory.CreateReadOnlyColumn(columnDef, width);
		}

		return _textCellFactory.CreateEditableColumn(columnDef, width);
	}

	private static bool IsGroupBasedComboBox(string columnType)
	{
		return string.Equals(columnType, ColumnTypes.ActionTargetComboBox, StringComparison.OrdinalIgnoreCase);
	}
}
