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

public sealed class ColumnBuilder
{
	private const string ActionColumnKey = "action";
	private const string ActionComboBoxType = "action_combo_box";
	private const string ActionTargetComboBoxType = "action_target_combo_box";

	private readonly IActionRegistry _actionRegistry;
	private readonly IGroupRegistry _groupRegistry;
	private GridStyleOptions _gridStyles;

	public ColumnBuilder(
		IActionRegistry actionRegistry,
		IGroupRegistry groupRegistry,
		GridStyleOptions gridStyles)
	{
		_actionRegistry = actionRegistry;
		_groupRegistry = groupRegistry;
		_gridStyles = gridStyles;
	}

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
			grid.Columns.Add(CreateColumn(columnDef));
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

		return CreateTextColumn(columnDef, width);
	}

	private static bool IsGroupBasedComboBox(string columnType)
	{
		return string.Equals(columnType, ActionTargetComboBoxType, StringComparison.OrdinalIgnoreCase);
	}

	private DataGridColumn CreateTextColumn(GridColumnDefinition columnDef, int width)
	{
		var column = new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Width = new DataGridLength(width),
			IsReadOnly = columnDef.ReadOnly,
			CanUserSort = false,
			CellTemplate = CreateTextCellTemplate(columnDef.Key, isEditing: false)
		};

		if (!columnDef.ReadOnly)
		{
			column.CellEditingTemplate = CreateTextCellTemplate(columnDef.Key, isEditing: true);
		}

		return column;
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateTextCellTemplate(string columnKey, bool isEditing)
	{
		return new FuncDataTemplate<RecipeRowViewModel>((row, _) => BuildTextCell(row, columnKey, isEditing));
	}

	private Control BuildTextCell(RecipeRowViewModel? row, string columnKey, bool isEditing)
	{
		if (row is null)
		{
			return CreateEmptyTextBlock();
		}

		var value = row.GetPropertyValue(columnKey)?.ToString() ?? string.Empty;

		if (!isEditing)
		{
			return CreateStyledTextBlock(row, columnKey, value);
		}

		return CreateStyledTextBox(row, columnKey, value);
	}

	private TextBlock CreateStyledTextBlock(RecipeRowViewModel row, string columnKey, string text)
	{
		var cellState = row.CellStates.TryGetValue(columnKey, out var state) ? state : CellState.Enabled;
		var isSelected = row.IsSelected;

		var backgroundColor = GetBackgroundColor(cellState, isSelected);
		var foregroundColor = GetForegroundColor(isSelected);

		var textBlock = new TextBlock
		{
			Text = text,
			FontSize = _gridStyles.CellFontSize,
			Padding = GetCellPadding(),
			VerticalAlignment = VerticalAlignment.Center,
			Background = StyleHelper.ToBrush(backgroundColor),
			Foreground = StyleHelper.ToBrush(foregroundColor)
		};

		return textBlock;
	}

	private TextBox CreateStyledTextBox(RecipeRowViewModel row, string columnKey, string value)
	{
		var cellState = row.CellStates.TryGetValue(columnKey, out var state) ? state : CellState.Enabled;
		var isSelected = row.IsSelected;

		var backgroundColor = GetBackgroundColor(cellState, isSelected);
		var foregroundColor = GetForegroundColor(isSelected);

		var textBox = new TextBox
		{
			Text = value,
			FontSize = _gridStyles.CellFontSize,
			Padding = GetCellPadding(),
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			Background = StyleHelper.ToBrush(backgroundColor),
			Foreground = StyleHelper.ToBrush(foregroundColor)
		};

		textBox.LostFocus += (_, _) =>
		{
			row.SetPropertyValue(columnKey, textBox.Text);
		};

		return textBox;
	}

	private DataGridColumn CreateActionColumn(GridColumnDefinition columnDef, int width)
	{
		var column = new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Width = new DataGridLength(width),
			IsReadOnly = columnDef.ReadOnly,
			CanUserSort = false,
			CellTemplate = CreateActionTemplate(isEditing: false)
		};

		if (!columnDef.ReadOnly)
		{
			column.CellEditingTemplate = CreateActionTemplate(isEditing: true);
		}

		return column;
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateActionTemplate(bool isEditing)
	{
		return new FuncDataTemplate<RecipeRowViewModel>((row, _) => BuildActionCell(row, isEditing));
	}

	private Control BuildActionCell(RecipeRowViewModel? row, bool isEditing)
	{
		if (row is null)
		{
			return CreateEmptyTextBlock();
		}

		if (!isEditing)
		{
			return CreateStyledTextBlock(row, ActionColumnKey, row.ActionName);
		}

		return CreateActionComboBox(row);
	}

	private ComboBox CreateActionComboBox(RecipeRowViewModel row)
	{
		var actions = _actionRegistry?.GetAll() ?? [];
		var items = actions
			.Select(a => new ActionComboBoxItemViewModel(a.Id, a.UiName))
			.ToList();

		var comboBox = new ComboBox
		{
			ItemsSource = items,
			DisplayMemberBinding = new Binding("DisplayText"),
			FontSize = _gridStyles.CellFontSize,
			Padding = GetCellPadding(),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Center,
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

		return comboBox;
	}

	private DataGridColumn CreateGroupComboBoxColumn(GridColumnDefinition columnDef, int width)
	{
		var column = new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Width = new DataGridLength(width),
			IsReadOnly = columnDef.ReadOnly,
			CanUserSort = false,
			CellTemplate = CreateGroupComboBoxTemplate(columnDef.Key, isEditing: false)
		};

		if (!columnDef.ReadOnly)
		{
			column.CellEditingTemplate = CreateGroupComboBoxTemplate(columnDef.Key, isEditing: true);
		}

		return column;
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateGroupComboBoxTemplate(string columnKey, bool isEditing)
	{
		return new FuncDataTemplate<RecipeRowViewModel>((row, _) => BuildGroupComboBoxCell(row, columnKey, isEditing));
	}

	private Control BuildGroupComboBoxCell(RecipeRowViewModel? row, string columnKey, bool isEditing)
	{
		if (row is null)
		{
			return CreateEmptyTextBlock();
		}

		var cellState = row.CellStates.TryGetValue(columnKey, out var state) ? state : CellState.Enabled;

		if (cellState == CellState.Disabled)
		{
			return CreateDisabledGroupCell(row);
		}

		if (!isEditing || cellState == CellState.Readonly)
		{
			return CreateReadonlyGroupCell(row, columnKey, cellState);
		}

		return CreateGroupComboBox(row, columnKey);
	}

	private TextBlock CreateDisabledGroupCell(RecipeRowViewModel row)
	{
		var backgroundColor = GetBackgroundColor(CellState.Disabled, row.IsSelected);
		var foregroundColor = GetForegroundColor(row.IsSelected);

		return new TextBlock
		{
			Text = string.Empty,
			FontSize = _gridStyles.CellFontSize,
			Padding = GetCellPadding(),
			VerticalAlignment = VerticalAlignment.Center,
			Background = StyleHelper.ToBrush(backgroundColor),
			Foreground = StyleHelper.ToBrush(foregroundColor)
		};
	}

	private TextBlock CreateReadonlyGroupCell(RecipeRowViewModel row, string columnKey, CellState cellState)
	{
		var groupItems = row.GetGroupItemsForColumn(columnKey);
		var currentValue = row.GetPropertyValue(columnKey);
		var displayText = GetGroupItemDisplayText(groupItems, currentValue);

		var backgroundColor = GetBackgroundColor(cellState, row.IsSelected);
		var foregroundColor = GetForegroundColor(row.IsSelected);

		return new TextBlock
		{
			Text = displayText,
			FontSize = _gridStyles.CellFontSize,
			Padding = GetCellPadding(),
			VerticalAlignment = VerticalAlignment.Center,
			Background = StyleHelper.ToBrush(backgroundColor),
			Foreground = StyleHelper.ToBrush(foregroundColor)
		};
	}

	private ComboBox CreateGroupComboBox(RecipeRowViewModel row, string columnKey)
	{
		var groupItems = row.GetGroupItemsForColumn(columnKey);
		var items = CreateGroupComboBoxItems(groupItems);

		var comboBox = new ComboBox
		{
			ItemsSource = items,
			DisplayMemberBinding = new Binding("DisplayText"),
			FontSize = _gridStyles.CellFontSize,
			Padding = GetCellPadding(),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Center,
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

		return comboBox;
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

	private Thickness GetCellPadding()
	{
		return StyleHelper.ToThickness(
			_gridStyles.CellPaddingLeft,
			_gridStyles.CellPaddingTop,
			_gridStyles.CellPaddingRight,
			_gridStyles.CellPaddingBottom);
	}

	private string GetBackgroundColor(CellState cellState, bool isSelected)
	{
		return cellState switch
		{
			CellState.Enabled => isSelected ? _gridStyles.EnabledCellSelectedColor : _gridStyles.EnabledCellNormalColor,
			CellState.Readonly => isSelected ? _gridStyles.ReadonlyCellSelectedColor : _gridStyles.ReadonlyCellNormalColor,
			CellState.Disabled => isSelected ? _gridStyles.DisabledCellSelectedColor : _gridStyles.DisabledCellNormalColor,
			_ => _gridStyles.EnabledCellNormalColor
		};
	}

	private string GetForegroundColor(bool isSelected)
	{
		return isSelected ? _gridStyles.SelectionForegroundColor : _gridStyles.NormalForegroundColor;
	}
}

public sealed record ActionComboBoxItemViewModel(short Id, string DisplayText);

public sealed record GroupComboBoxItemViewModel(int Id, string DisplayText);
