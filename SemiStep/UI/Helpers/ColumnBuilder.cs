using Avalonia.Controls;
using Avalonia.Data;

using Shared;
using Shared.Entities;
using Shared.Registries;

namespace UI.Helpers;

public sealed class ColumnBuilder(
	IActionRegistry actionRegistry,
	IGroupRegistry groupRegistry,
	GridStyleOptions gridStyle,
	IPropertyRegistry propertyRegistry,
	IColumnRegistry columnRegistry)
{
	private const string ActionColumnKey = "action";
	private const string ActionTargetComboBoxType = "action_target_combo_box";

	private readonly TextCellFactory _textCellFactory = new(propertyRegistry, columnRegistry);
	private readonly ComboBoxCellFactory _comboBoxCellFactory = new(actionRegistry);
	private readonly ColumnWidthCalculator _widthCalculator = new(actionRegistry, groupRegistry, gridStyle);

	public void BuildColumnsFromConfiguration(DataGrid grid, AppConfiguration config)
	{
		grid.Columns.Clear();
		_comboBoxCellFactory.InvalidateActionCache();
		AddNumberingColumn(grid);
		AddColumnsFromConfig(grid, config);
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

		if (columnDef.Key == ActionColumnKey)
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
		return string.Equals(columnType, ActionTargetComboBoxType, StringComparison.OrdinalIgnoreCase);
	}
}
