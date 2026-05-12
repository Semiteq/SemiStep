using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

public sealed class ComboBoxCellFactory(RecipeMetadataRegistry recipeMetadataRegistry)
{
	private readonly Dictionary<string, List<ComboBoxItemViewModel>> _groupItemsByGroupName = new();
	private List<ComboBoxItemViewModel>? _cachedActionItems;

	public void InvalidateCaches()
	{
		_cachedActionItems = null;
		_groupItemsByGroupName.Clear();
	}

	public DataGridColumn CreateActionColumn(GridColumnDefinition columnDef, DataGridLength width)
	{
		return new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Tag = columnDef.Key,
			Width = width,
			IsReadOnly = false,
			CanUserSort = false,
			CellTemplate = CreateActionDisplayTemplate(),
			CellEditingTemplate = CreateActionEditingTemplate(columnDef.ReadOnly)
		};
	}

	public DataGridColumn CreateGroupComboBoxColumn(GridColumnDefinition columnDef, DataGridLength width)
	{
		return new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Tag = columnDef.Key,
			Width = width,
			IsReadOnly = false,
			CanUserSort = false,
			CellTemplate = CreateGroupDisplayTemplate(columnDef.Key),
			CellEditingTemplate = CreateGroupEditingTemplate(columnDef.Key, columnDef.ReadOnly)
		};
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateActionDisplayTemplate()
	{
		var cellStateConverter = new CellStateConverter(ColumnTypes.Action);

		return new FuncDataTemplate<RecipeRowViewModel>((row, _) =>
		{
			var textBlock = new TextBlock
			{
				VerticalAlignment = VerticalAlignment.Center,
				Padding = new Thickness(4, 2),
				TextAlignment = TextAlignment.Center,
			};

			if (row is not null)
			{
				textBlock.Text = row.ActionName;
			}

			return CellPresenter.Wrap(textBlock, cellStateConverter);
		}, supportsRecycling: false);
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateActionEditingTemplate(bool isColumnReadOnly)
	{
		var items = GetOrCreateActionItems();
		var cellStateConverter = new CellStateConverter(ColumnTypes.Action);

		return new FuncDataTemplate<RecipeRowViewModel>((row, _) =>
		{
			var currentId = row?.ActionId ?? 0;
			var selectedItem = items.FirstOrDefault(item => item.Id == currentId);
			var isEnabled = !isColumnReadOnly;

			var comboBox = new ComboBox
			{
				ItemsSource = items,
				DisplayMemberBinding = new Binding("DisplayText"),
				SelectedItem = selectedItem,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Center,
				Background = Brushes.Transparent,
				BorderThickness = new Thickness(0),
				IsHitTestVisible = isEnabled,
			};

			if (isEnabled && row is not null)
			{
				comboBox.SelectionChanged += (_, _) =>
				{
					if (comboBox.SelectedItem is ComboBoxItemViewModel selected)
					{
						row.SetPropertyValue(ColumnTypes.Action, selected.Id.ToString());
					}
				};
			}

			return CellPresenter.Wrap(comboBox, cellStateConverter);
		}, supportsRecycling: false);
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateGroupDisplayTemplate(string columnKey)
	{
		var cellStateConverter = new CellStateConverter(columnKey);

		return new FuncDataTemplate<RecipeRowViewModel>((row, _) =>
		{
			var textBlock = new TextBlock
			{
				VerticalAlignment = VerticalAlignment.Center,
				Padding = new Thickness(4, 2),
				TextAlignment = TextAlignment.Center,
			};

			if (row is not null)
			{
				var displayText = ResolveGroupDisplayText(row, columnKey);
				textBlock.Text = displayText;
			}

			return CellPresenter.Wrap(textBlock, cellStateConverter);
		}, supportsRecycling: false);
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateGroupEditingTemplate(string columnKey, bool isColumnReadOnly)
	{
		var cellStateConverter = new CellStateConverter(columnKey);

		return new FuncDataTemplate<RecipeRowViewModel>((row, _) =>
		{
			if (row is null)
			{
				return CellPresenter.Wrap(new TextBlock { Text = string.Empty }, cellStateConverter);
			}

			var groupItems = GetOrCreateGroupItems(row, columnKey);
			var selectionConverter = new ComboBoxItemSelectionConverter(groupItems);
			var cellState = row.CellStates.TryGetValue(columnKey, out var state) ? state : CellState.Enabled;
			var isEnabled = !isColumnReadOnly && cellState == CellState.Enabled;

			var comboBox = new ComboBox
			{
				ItemsSource = groupItems,
				DisplayMemberBinding = new Binding("DisplayText"),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Center,
				Background = Brushes.Transparent,
				BorderThickness = new Thickness(0),
				IsHitTestVisible = isEnabled,
			};
			comboBox.Bind(ComboBox.SelectedItemProperty,
				new Binding($"[{columnKey}]") { Mode = BindingMode.TwoWay, Converter = selectionConverter });

			return CellPresenter.Wrap(comboBox, cellStateConverter);
		}, supportsRecycling: false);
	}

	private string ResolveGroupDisplayText(RecipeRowViewModel row, string columnKey)
	{
		if (row.GetPropertyValue(columnKey) is not int intValue)
		{
			return string.Empty;
		}

		var groupItems = row.GetGroupItemsForColumn(columnKey);
		if (groupItems is null)
		{
			return string.Empty;
		}

		return groupItems.TryGetValue(intValue, out var displayText) ? displayText : string.Empty;
	}

	private List<ComboBoxItemViewModel> GetOrCreateGroupItems(RecipeRowViewModel row, string columnKey)
	{
		var groupName = row.GetGroupNameForColumn(columnKey);
		if (groupName is null)
		{
			return [];
		}

		if (_groupItemsByGroupName.TryGetValue(groupName, out var cached))
		{
			return cached;
		}

		var groupResult = recipeMetadataRegistry.GetGroup(groupName);
		if (groupResult.IsFailed)
		{
			return [];
		}

		var items = groupResult.Value.Items
			.Select(kvp => new ComboBoxItemViewModel(kvp.Key, kvp.Value))
			.OrderBy(item => item.Id)
			.ToList();

		_groupItemsByGroupName[groupName] = items;

		return items;
	}

	private List<ComboBoxItemViewModel> GetOrCreateActionItems()
	{
		if (_cachedActionItems is not null)
		{
			return _cachedActionItems;
		}

		_cachedActionItems = recipeMetadataRegistry.GetAllActions()
			.Select(a => new ComboBoxItemViewModel(a.Id, a.UiName))
			.ToList();

		return _cachedActionItems;
	}
}
