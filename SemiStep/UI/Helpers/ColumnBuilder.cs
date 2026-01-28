using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;

using Shared;
using Shared.Entities;
using Shared.Registries;

using UI.ViewModels;

namespace UI.Helpers;

public sealed class ColumnBuilder(
	IActionRegistry actionRegistry,
	GridStyleOptions gridStyles)
{
	private const string ActionColumnKey = "action";
	private const string ActionTargetComboBoxType = "action_target_combo_box";

	private const string CellEnabledClass = "cell-enabled";
	private const string CellReadonlyClass = "cell-readonly";
	private const string CellDisabledClass = "cell-disabled";
	private const string SelectedClass = "selected";

	private GridStyleOptions _gridStyles = gridStyles;

	public void UpdateStyles(GridStyleOptions gridStyles)
	{
		_gridStyles = gridStyles;
	}

	public void BuildColumnsFromConfiguration(DataGrid grid, AppConfiguration config)
	{
		grid.Columns.Clear();
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
		foreach (var columnDef in config.Columns.Values.OrderBy(c => c.Key))
		{
			var column = CreateColumn(columnDef);
			grid.Columns.Add(column);
		}
	}

	private DataGridColumn CreateColumn(GridColumnDefinition columnDef)
	{
		var width = columnDef.Width > 0 ? columnDef.Width : 100;

		if (columnDef.Key == ActionColumnKey)
		{
			return CreateActionColumn(columnDef, width);
		}

		if (IsGroupBasedComboBox(columnDef.ColumnType))
		{
			return CreateGroupComboBoxColumn(columnDef, width);
		}

		if (columnDef.ReadOnly)
		{
			return CreateReadOnlyTextColumn(columnDef, width);
		}

		return CreateTextColumn(columnDef, width);
	}

	private static bool IsGroupBasedComboBox(string columnType)
	{
		return string.Equals(columnType, ActionTargetComboBoxType, StringComparison.OrdinalIgnoreCase);
	}

	private static DataGridColumn CreateReadOnlyTextColumn(GridColumnDefinition columnDef, int width)
	{
		return new DataGridTextColumn
		{
			Header = columnDef.UiName,
			Width = new DataGridLength(width),
			IsReadOnly = true,
			CanUserSort = false,
			Binding = new Binding($"[{columnDef.Key}]")
		};
	}

	private static DataGridColumn CreateTextColumn(GridColumnDefinition columnDef, int width)
	{
		return new DataGridTextColumn
		{
			Header = columnDef.UiName,
			Width = new DataGridLength(width),
			IsReadOnly = false,
			CanUserSort = false,
			Binding = new Binding($"[{columnDef.Key}]") { Mode = BindingMode.TwoWay }
		};
	}

	private DataGridColumn CreateActionColumn(GridColumnDefinition columnDef, int width)
	{
		var column = new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Width = new DataGridLength(width),
			IsReadOnly = false,
			CanUserSort = false,
			CellTemplate = CreateActionTemplate(isEditing: false, isColumnReadOnly: columnDef.ReadOnly),
			CellEditingTemplate = CreateActionTemplate(isEditing: true, isColumnReadOnly: columnDef.ReadOnly)
		};

		return column;
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateActionTemplate(bool isEditing, bool isColumnReadOnly)
	{
		return new FuncDataTemplate<RecipeRowViewModel>(
			(row, _) => BuildActionCell(row, isEditing, isColumnReadOnly),
			supportsRecycling: false);
	}

	private Control BuildActionCell(RecipeRowViewModel? row, bool isEditing, bool isColumnReadOnly)
	{
		if (row is null)
		{
			return CreateEmptyTextBlock();
		}

		if (!isEditing || isColumnReadOnly)
		{
			return CreateDisplayTextBlock(row.ActionName);
		}

		return CreateActionComboBox(row);
	}

	private Control CreateActionComboBox(RecipeRowViewModel row)
	{
		var actions = actionRegistry.GetAll();
		var items = actions
			.Select(a => new ActionComboBoxItemViewModel(a.Id, a.UiName))
			.ToList();

		var border = new Border
		{
			Padding = GetCellPadding(),
			VerticalAlignment = VerticalAlignment.Stretch,
			HorizontalAlignment = HorizontalAlignment.Stretch
		};

		var comboBox = new ComboBox
		{
			ItemsSource = items,
			DisplayMemberBinding = new Binding("DisplayText"),
			FontSize = _gridStyles.CellFontSize,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Center,
			Background = Avalonia.Media.Brushes.Transparent,
			BorderThickness = new Thickness(0),
			IsEnabled = true
		};

		var currentActionId = row.ActionId;
		comboBox.SelectedItem = items.FirstOrDefault(item => item.Id == currentActionId);

		comboBox.SelectionChanged += (_, _) =>
		{
			if (comboBox.SelectedItem is ActionComboBoxItemViewModel selectedItem)
			{
				row.SetPropertyValue(ActionColumnKey, selectedItem.Id);
			}
		};

		border.Child = comboBox;

		ApplyCellClasses(border, row, ActionColumnKey);
		SubscribeToRowChanges(border, row, ActionColumnKey);

		return border;
	}

	private DataGridColumn CreateGroupComboBoxColumn(GridColumnDefinition columnDef, int width)
	{
		var column = new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Width = new DataGridLength(width),
			IsReadOnly = false,
			CanUserSort = false,
			CellTemplate = CreateGroupComboBoxTemplate(columnDef.Key, isEditing: false, isColumnReadOnly: columnDef.ReadOnly),
			CellEditingTemplate = CreateGroupComboBoxTemplate(columnDef.Key, isEditing: true, isColumnReadOnly: columnDef.ReadOnly)
		};

		return column;
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateGroupComboBoxTemplate(string columnKey, bool isEditing, bool isColumnReadOnly)
	{
		return new FuncDataTemplate<RecipeRowViewModel>(
			(row, _) => BuildGroupComboBoxCell(row, columnKey, isEditing, isColumnReadOnly),
			supportsRecycling: false);
	}

	private Control BuildGroupComboBoxCell(RecipeRowViewModel? row, string columnKey, bool isEditing, bool isColumnReadOnly)
	{
		if (row is null)
		{
			return CreateEmptyTextBlock();
		}

		var cellState = row.CellStates.TryGetValue(columnKey, out var state) ? state : CellState.Enabled;

		if (cellState == CellState.Disabled)
		{
			return CreateEmptyTextBlock();
		}

		if (!isEditing || isColumnReadOnly || cellState == CellState.Readonly)
		{
			var groupItems = row.GetGroupItemsForColumn(columnKey);
			var currentValue = row.GetPropertyValue(columnKey);
			var displayText = GetGroupItemDisplayText(groupItems, currentValue);
			return CreateDisplayTextBlock(displayText);
		}

		return CreateGroupComboBox(row, columnKey);
	}

	private Control CreateGroupComboBox(RecipeRowViewModel row, string columnKey)
	{
		var groupItems = row.GetGroupItemsForColumn(columnKey);
		var items = CreateGroupComboBoxItems(groupItems);

		var border = new Border
		{
			Padding = GetCellPadding(),
			VerticalAlignment = VerticalAlignment.Stretch,
			HorizontalAlignment = HorizontalAlignment.Stretch
		};

		var comboBox = new ComboBox
		{
			ItemsSource = items,
			DisplayMemberBinding = new Binding("DisplayText"),
			FontSize = _gridStyles.CellFontSize,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Center,
			Background = Avalonia.Media.Brushes.Transparent,
			BorderThickness = new Thickness(0),
			IsEnabled = true
		};

		var currentValue = row.GetPropertyValue(columnKey);
		if (currentValue is int intValue)
		{
			comboBox.SelectedItem = items.FirstOrDefault(item => item.Id == intValue);
		}

		comboBox.SelectionChanged += (_, _) =>
		{
			if (comboBox.SelectedItem is GroupComboBoxItemViewModel selectedItem)
			{
				row.SetPropertyValue(columnKey, selectedItem.Id);
			}
		};

		border.Child = comboBox;

		ApplyCellClasses(border, row, columnKey);
		SubscribeToRowChanges(border, row, columnKey);

		return border;
	}

	private static List<GroupComboBoxItemViewModel> CreateGroupComboBoxItems(IReadOnlyDictionary<int, string>? groupItems)
	{
		if (groupItems is null)
		{
			return [];
		}

		return groupItems
			.Select(kvp => new GroupComboBoxItemViewModel(kvp.Key, kvp.Value))
			.OrderBy(item => item.Id)
			.ToList();
	}

	private static string GetGroupItemDisplayText(IReadOnlyDictionary<int, string>? groupItems, object? value)
	{
		if (groupItems is null || value is null)
		{
			return string.Empty;
		}

		if (value is not int intValue)
		{
			return value.ToString() ?? string.Empty;
		}

		return groupItems.TryGetValue(intValue, out var displayText)
			? displayText
			: intValue.ToString();
	}

	private static TextBlock CreateEmptyTextBlock()
	{
		return new TextBlock { Text = string.Empty };
	}

	private static TextBlock CreateDisplayTextBlock(string text)
	{
		return new TextBlock
		{
			Text = text,
			VerticalAlignment = VerticalAlignment.Center
		};
	}

	private Thickness GetCellPadding()
	{
		return StyleHelper.ToThickness(
			_gridStyles.CellPaddingLeft,
			_gridStyles.CellPaddingTop,
			_gridStyles.CellPaddingRight,
			_gridStyles.CellPaddingBottom);
	}

	private static void ApplyCellClasses(Control control, RecipeRowViewModel row, string columnKey)
	{
		control.Classes.Remove(CellEnabledClass);
		control.Classes.Remove(CellReadonlyClass);
		control.Classes.Remove(CellDisabledClass);

		var cellState = row.CellStates.TryGetValue(columnKey, out var state) ? state : CellState.Enabled;
		var stateClass = GetCellStateClass(cellState);

		control.Classes.Add(stateClass);
		control.Classes.Set(SelectedClass, row.IsSelected);
	}

	private static string GetCellStateClass(CellState cellState)
	{
		return cellState switch
		{
			CellState.Enabled => CellEnabledClass,
			CellState.Readonly => CellReadonlyClass,
			CellState.Disabled => CellDisabledClass,
			_ => CellEnabledClass
		};
	}

	private static void SubscribeToRowChanges(Control control, RecipeRowViewModel row, string columnKey)
	{
		row.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(RecipeRowViewModel.IsSelected) ||
				e.PropertyName == nameof(RecipeRowViewModel.CellStates))
			{
				ApplyCellClasses(control, row, columnKey);
			}
		};
	}
}

public sealed record ActionComboBoxItemViewModel(int Id, string DisplayText);

public sealed record GroupComboBoxItemViewModel(int Id, string DisplayText);
