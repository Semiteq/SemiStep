using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

using Shared.Entities;
using Shared.Registries;

using UI.Controls;
using UI.ViewModels;

namespace UI.Helpers;

public sealed class ComboBoxCellFactory(IActionRegistry actionRegistry)
{
	private const string ActionColumnKey = "action";

	private List<ActionComboBoxItemViewModel>? _cachedActionItems;

	public void InvalidateActionCache()
	{
		_cachedActionItems = null;
	}

	public DataGridColumn CreateActionColumn(GridColumnDefinition columnDef, int width)
	{
		return new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Width = new DataGridLength(width),
			IsReadOnly = false,
			CanUserSort = false,
			CellTemplate = CreateActionTemplate(isEditing: false, isColumnReadOnly: columnDef.ReadOnly),
			CellEditingTemplate = CreateActionTemplate(isEditing: true, isColumnReadOnly: columnDef.ReadOnly)
		};
	}

	public DataGridColumn CreateGroupComboBoxColumn(GridColumnDefinition columnDef, int width)
	{
		return new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Width = new DataGridLength(width),
			IsReadOnly = false,
			CanUserSort = false,
			CellTemplate = CreateGroupTemplate(columnDef.Key, isEditing: false, isColumnReadOnly: columnDef.ReadOnly),
			CellEditingTemplate = CreateGroupTemplate(columnDef.Key, isEditing: true, isColumnReadOnly: columnDef.ReadOnly)
		};
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateActionTemplate(bool isEditing, bool isColumnReadOnly)
	{
		return new FuncDataTemplate<RecipeRowViewModel>(
			(row, _) => BuildActionCell(row, isEditing, isColumnReadOnly),
			supportsRecycling: false);
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateGroupTemplate(
		string columnKey, bool isEditing, bool isColumnReadOnly)
	{
		return new FuncDataTemplate<RecipeRowViewModel>(
			(row, _) => BuildGroupCell(row, columnKey, isEditing, isColumnReadOnly),
			supportsRecycling: false);
	}

	private Control BuildActionCell(RecipeRowViewModel? row, bool isEditing, bool isColumnReadOnly)
	{
		if (row is null)
		{
			return CreateEmptyTextBlock();
		}

		var isEnabled = isEditing && !isColumnReadOnly;
		var items = GetOrCreateActionItems();
		var currentId = row.ActionId;
		var selectedItem = items.FirstOrDefault(item => item.Id == currentId);

		return CreateComboBoxCell(
			row,
			ActionColumnKey,
			items,
			selectedItem,
			isEnabled,
			comboBox =>
			{
				if (isEnabled)
				{
					comboBox.SelectionChanged += (_, _) =>
					{
						if (comboBox.SelectedItem is ActionComboBoxItemViewModel selected)
						{
							row.SetPropertyValue(ActionColumnKey, selected.Id);
						}
					};
				}
			});
	}

	private Control BuildGroupCell(
		RecipeRowViewModel? row, string columnKey, bool isEditing, bool isColumnReadOnly)
	{
		if (row is null)
		{
			return CreateEmptyTextBlock();
		}

		var cellState = row.CellStates.TryGetValue(columnKey, out var state) ? state : CellState.Enabled;
		var isEnabled = isEditing && !isColumnReadOnly && cellState == CellState.Enabled;
		var items = CreateGroupItems(row.GetGroupItemsForColumn(columnKey));

		object? selectedItem = null;
		if (row.GetPropertyValue(columnKey) is int intValue)
		{
			selectedItem = items.FirstOrDefault(item => item.Id == intValue);
		}

		return CreateComboBoxCell(
			row,
			columnKey,
			items,
			selectedItem,
			isEnabled,
			comboBox =>
			{
				if (isEnabled)
				{
					comboBox.SelectionChanged += (_, _) =>
					{
						if (comboBox.SelectedItem is GroupComboBoxItemViewModel selected)
						{
							row.SetPropertyValue(columnKey, selected.Id);
						}
					};
				}
			});
	}

	private static Control CreateComboBoxCell(
		RecipeRowViewModel row,
		string columnKey,
		object items,
		object? selectedItem,
		bool isEnabled,
		Action<ComboBox> wireSelectionChanged)
	{
		var cellState = row.CellStates.TryGetValue(columnKey, out var state) ? state : CellState.Enabled;

		var comboBox = new ComboBox
		{
			ItemsSource = items as System.Collections.IEnumerable,
			DisplayMemberBinding = new Binding("DisplayText"),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Center,
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0),
			IsHitTestVisible = isEnabled
		};

		comboBox.SelectedItem = selectedItem;
		wireSelectionChanged(comboBox);

		comboBox.Classes.Set("cell-disabled", cellState == CellState.Disabled);

		var presenter = new CellPresenter
		{
			CellState = cellState,
			HorizontalContentAlignment = HorizontalAlignment.Stretch,
			VerticalContentAlignment = VerticalAlignment.Stretch,
			Content = comboBox
		};

		SubscribeWithLifecycle(presenter, row, columnKey, comboBox);
		return presenter;
	}

	private List<ActionComboBoxItemViewModel> GetOrCreateActionItems()
	{
		if (_cachedActionItems is not null)
		{
			return _cachedActionItems;
		}

		var actions = actionRegistry.GetAll();
		_cachedActionItems = actions
			.Select(a => new ActionComboBoxItemViewModel(a.Id, a.UiName))
			.ToList();

		return _cachedActionItems;
	}

	private static List<GroupComboBoxItemViewModel> CreateGroupItems(
		IReadOnlyDictionary<int, string>? groupItems)
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

	private static void SubscribeWithLifecycle(
		CellPresenter presenter, RecipeRowViewModel row, string columnKey,
		ComboBox comboBox)
	{
		PropertyChangedEventHandler handler = (_, e) =>
		{
			if (e.PropertyName is nameof(RecipeRowViewModel.CellStates))
			{
				var newState = row.CellStates.TryGetValue(columnKey, out var s) ? s : CellState.Enabled;
				presenter.CellState = newState;
				comboBox.Classes.Set("cell-disabled", newState == CellState.Disabled);
			}
		};

		row.PropertyChanged += handler;

		presenter.DetachedFromVisualTree += OnDetached;
		presenter.AttachedToVisualTree += OnAttached;
		return;

		void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
		{
			row.PropertyChanged -= handler;
		}

		void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
		{
			row.PropertyChanged -= handler;
			row.PropertyChanged += handler;
		}
	}

	private static TextBlock CreateEmptyTextBlock()
	{
		return new TextBlock { Text = string.Empty };
	}
}
