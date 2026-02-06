using System.ComponentModel;

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
	IActionRegistry actionRegistry)
{
	private const string ActionColumnKey = "action";
	private const string ActionTargetComboBoxType = "action_target_combo_box";

	private const string CellEnabledClass = "cell-enabled";
	private const string CellReadonlyClass = "cell-readonly";
	private const string CellDisabledClass = "cell-disabled";
	private const string SelectedClass = "selected";

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
			Header = "№",
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
		return new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Width = new DataGridLength(width),
			IsReadOnly = true,
			CanUserSort = false,
			CellTemplate = CreateTextCellTemplate(columnDef.Key, isEditing: false, isColumnReadOnly: true)
		};
	}

	private static DataGridColumn CreateTextColumn(GridColumnDefinition columnDef, int width)
	{
		return new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Width = new DataGridLength(width),
			IsReadOnly = false,
			CanUserSort = false,
			CellTemplate = CreateTextCellTemplate(columnDef.Key, isEditing: false, isColumnReadOnly: false),
			CellEditingTemplate = CreateTextCellTemplate(columnDef.Key, isEditing: true, isColumnReadOnly: false)
		};
	}

	private static FuncDataTemplate<RecipeRowViewModel> CreateTextCellTemplate(string columnKey, bool isEditing, bool isColumnReadOnly)
	{
		return new FuncDataTemplate<RecipeRowViewModel>(
			(row, _) => BuildTextCell(row, columnKey, isEditing, isColumnReadOnly),
			supportsRecycling: false);
	}

	private static Control BuildTextCell(RecipeRowViewModel? row, string columnKey, bool isEditing, bool isColumnReadOnly)
	{
		if (row is null)
		{
			return CreateEmptyTextBlock();
		}

		var cellState = row.CellStates.TryGetValue(columnKey, out var state) ? state : CellState.Enabled;

		if (cellState == CellState.Disabled)
		{
			var disabledBlock = CreateEmptyTextBlock();
			ApplyCellClasses(disabledBlock, row, columnKey);
			SubscribeToRowChanges(disabledBlock, row, columnKey);
			return disabledBlock;
		}

		if (!isEditing || isColumnReadOnly || cellState == CellState.Readonly)
		{
			var value = row.GetPropertyValue(columnKey);
			var displayText = value?.ToString() ?? string.Empty;
			var textBlock = new TextBlock
			{
				Text = displayText,
				VerticalAlignment = VerticalAlignment.Center,
				Padding = new Thickness(4, 2)
			};
			ApplyCellClasses(textBlock, row, columnKey);
			SubscribeToRowChanges(textBlock, row, columnKey);
			return textBlock;
		}

		// Editable text box
		var textBox = new TextBox
		{
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			BorderThickness = new Thickness(0),
			Padding = new Thickness(4, 2)
		};

		// Bind to the indexer
		textBox.Bind(TextBox.TextProperty, new Binding($"[{columnKey}]") { Mode = BindingMode.TwoWay });

		ApplyCellClasses(textBox, row, columnKey);
		SubscribeToRowChanges(textBox, row, columnKey);

		return textBox;
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

		// Always show ComboBox so dropdown arrow is visible
		var isEnabled = isEditing && !isColumnReadOnly;
		return CreateActionComboBox(row, isEnabled);
	}

	private Control CreateActionComboBox(RecipeRowViewModel row, bool isEnabled)
	{
		var actions = actionRegistry.GetAll();
		var items = actions
			.Select(a => new ActionComboBoxItemViewModel(a.Id, a.UiName))
			.ToList();

		var border = new Border
		{
			VerticalAlignment = VerticalAlignment.Stretch,
			HorizontalAlignment = HorizontalAlignment.Stretch
		};

		var comboBox = new ComboBox
		{
			ItemsSource = items,
			DisplayMemberBinding = new Binding("DisplayText"),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Center,
			Background = Avalonia.Media.Brushes.Transparent,
			BorderThickness = new Thickness(0),
			IsHitTestVisible = isEnabled // Use IsHitTestVisible to prevent interaction without graying out
		};

		var currentActionId = row.ActionId;
		comboBox.SelectedItem = items.FirstOrDefault(item => item.Id == currentActionId);

		if (isEnabled)
		{
			comboBox.SelectionChanged += (_, _) =>
			{
				if (comboBox.SelectedItem is ActionComboBoxItemViewModel selectedItem)
				{
					row.SetPropertyValue(ActionColumnKey, selectedItem.Id);
				}
			};
		}

		border.Child = comboBox;

		ApplyCellClasses(border, row, ActionColumnKey);
		ApplyComboBoxDisabledClass(comboBox, row, ActionColumnKey);
		SubscribeToRowChanges(border, row, ActionColumnKey, comboBox);

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

		// Determine if ComboBox should be enabled
		var isEnabled = isEditing && !isColumnReadOnly && cellState == CellState.Enabled;

		// Always show ComboBox so dropdown arrow is visible (even when disabled)
		return CreateGroupComboBox(row, columnKey, isEnabled, cellState);
	}

	private Control CreateGroupComboBox(RecipeRowViewModel row, string columnKey, bool isEnabled, CellState cellState)
	{
		var groupItems = row.GetGroupItemsForColumn(columnKey);
		var items = CreateGroupComboBoxItems(groupItems);

		var border = new Border
		{
			VerticalAlignment = VerticalAlignment.Stretch,
			HorizontalAlignment = HorizontalAlignment.Stretch
		};

		var comboBox = new ComboBox
		{
			ItemsSource = items,
			DisplayMemberBinding = new Binding("DisplayText"),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Center,
			Background = Avalonia.Media.Brushes.Transparent,
			BorderThickness = new Thickness(0),
			IsHitTestVisible = isEnabled // Use IsHitTestVisible to prevent interaction without graying out
		};

		var currentValue = row.GetPropertyValue(columnKey);
		if (currentValue is int intValue)
		{
			comboBox.SelectedItem = items.FirstOrDefault(item => item.Id == intValue);
		}

		if (isEnabled)
		{
			comboBox.SelectionChanged += (_, _) =>
			{
				if (comboBox.SelectedItem is GroupComboBoxItemViewModel selectedItem)
				{
					row.SetPropertyValue(columnKey, selectedItem.Id);
				}
			};
		}

		border.Child = comboBox;

		// Apply cell class based on cell state (for proper disabled styling)
		ApplyCellClassesWithState(border, row, cellState);
		ApplyComboBoxDisabledClassWithState(comboBox, cellState);
		SubscribeToRowChanges(border, row, columnKey, comboBox);

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

	private static TextBlock CreateEmptyTextBlock()
	{
		return new TextBlock { Text = string.Empty };
	}

	private static void ApplyCellClasses(Control control, RecipeRowViewModel row, string columnKey)
	{
		var cellState = row.CellStates.TryGetValue(columnKey, out var state) ? state : CellState.Enabled;
		ApplyCellClassesWithState(control, row, cellState);
	}

	private static void ApplyCellClassesWithState(Control control, RecipeRowViewModel row, CellState cellState)
	{
		control.Classes.Remove(CellEnabledClass);
		control.Classes.Remove(CellReadonlyClass);
		control.Classes.Remove(CellDisabledClass);

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

	private static void ApplyComboBoxDisabledClass(ComboBox comboBox, RecipeRowViewModel row, string columnKey)
	{
		var cellState = row.CellStates.TryGetValue(columnKey, out var state) ? state : CellState.Enabled;
		ApplyComboBoxDisabledClassWithState(comboBox, cellState);
	}

	private static void ApplyComboBoxDisabledClassWithState(ComboBox comboBox, CellState cellState)
	{
		// Only add disabled class when cell state is actually Disabled
		comboBox.Classes.Set(CellDisabledClass, cellState == CellState.Disabled);
	}

	private static void SubscribeToRowChanges(Control control, RecipeRowViewModel row, string columnKey, ComboBox? comboBox = null)
	{
		PropertyChangedEventHandler handler = (_, e) =>
		{
			if (e.PropertyName == nameof(RecipeRowViewModel.IsSelected) ||
				e.PropertyName == nameof(RecipeRowViewModel.CellStates))
			{
				ApplyCellClasses(control, row, columnKey);
				if (comboBox is not null)
				{
					ApplyComboBoxDisabledClass(comboBox, row, columnKey);
				}
			}
		};

		row.PropertyChanged += handler;

		// Register cleanup action to unsubscribe when row is disposed
		row.RegisterCleanup(() => row.PropertyChanged -= handler);
	}
}

public sealed record ActionComboBoxItemViewModel(int Id, string DisplayText);

public sealed record GroupComboBoxItemViewModel(int Id, string DisplayText);
