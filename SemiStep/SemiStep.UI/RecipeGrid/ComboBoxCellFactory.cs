using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

internal sealed class ComboBoxCellFactory(RecipeMetadataRegistry recipeMetadataRegistry)
{
	private readonly Dictionary<string, IReadOnlyList<ComboBoxItemViewModel>> _groupItemsByGroupName
		= new(StringComparer.OrdinalIgnoreCase);

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
			IsReadOnly = true,
			CanUserSort = false,
			CellTemplate = CreateActionCellTemplate(columnDef.ReadOnly),
		};
	}

	public DataGridColumn CreateGroupComboBoxColumn(GridColumnDefinition columnDef, DataGridLength width)
	{
		return new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Tag = columnDef.Key,
			Width = width,
			IsReadOnly = true,
			CanUserSort = false,
			CellTemplate = CreateGroupCellTemplate(columnDef.Key, columnDef.ReadOnly),
		};
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateActionCellTemplate(bool isColumnReadOnly)
	{
		var items = GetOrCreateActionItems();
		var selectionConverter = new ComboBoxItemSelectionConverter(items);

		return new FuncDataTemplate<RecipeRowViewModel>((_, _) =>
		{
			var comboBox = CreateStyledComboBox();
			comboBox.ItemsSource = items;

			comboBox.Bind(
				ComboBox.SelectedItemProperty,
				new Binding(ColumnTypes.IndexerPath(ColumnTypes.Action))
				{
					Mode = BindingMode.TwoWay,
					Converter = selectionConverter,
				});

			ApplyInputBlocking(comboBox, ColumnTypes.Action, isColumnReadOnly);

			return comboBox;
		}, supportsRecycling: true);
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateGroupCellTemplate(string columnKey, bool isColumnReadOnly)
	{
		var valueIndexerPath = ColumnTypes.IndexerPath(columnKey);

		return new FuncDataTemplate<RecipeRowViewModel>((row, _) =>
		{
			var comboBox = CreateStyledComboBox();
			var items = GetOrCreateGroupItems(row, columnKey);
			var selectionConverter = new ComboBoxItemSelectionConverter(items);

			comboBox.ItemsSource = items;

			comboBox.Bind(
				ComboBox.SelectedItemProperty,
				new Binding(valueIndexerPath)
				{
					Mode = BindingMode.TwoWay,
					Converter = selectionConverter,
				});

			ApplyInputBlocking(comboBox, columnKey, isColumnReadOnly);

			return comboBox;
		}, supportsRecycling: true);
	}

	private static ComboBox CreateStyledComboBox()
	{
		return new ComboBox
		{
			DisplayMemberBinding = new Binding("DisplayText"),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Center,
		};
	}

	private static void ApplyInputBlocking(ComboBox comboBox, string columnKey, bool isColumnReadOnly)
	{
		if (isColumnReadOnly)
		{
			comboBox.IsHitTestVisible = false;
			comboBox.Focusable = false;
			return;
		}

		comboBox.Bind(InputElement.IsHitTestVisibleProperty, CellApplicabilityBinding.CreateApplicableBinding(columnKey));
		comboBox.Bind(InputElement.FocusableProperty, CellApplicabilityBinding.CreateApplicableBinding(columnKey));
	}

	private List<ComboBoxItemViewModel> GetOrCreateActionItems()
	{
		if (_cachedActionItems is not null)
		{
			return _cachedActionItems;
		}

		_cachedActionItems = recipeMetadataRegistry.GetAllActions()
			.Select(action => new ComboBoxItemViewModel(action.Id, action.UiName))
			.ToList();

		return _cachedActionItems;
	}

	private IReadOnlyList<ComboBoxItemViewModel> GetOrCreateGroupItems(RecipeRowViewModel? row, string columnKey)
	{
		var groupName = row?.GetGroupNameForColumn(columnKey);
		if (groupName is null)
		{
			return Array.Empty<ComboBoxItemViewModel>();
		}

		if (_groupItemsByGroupName.TryGetValue(groupName, out var cached))
		{
			return cached;
		}

		var groupResult = recipeMetadataRegistry.GetGroup(groupName);
		if (groupResult.IsFailed)
		{
			return Array.Empty<ComboBoxItemViewModel>();
		}

		var items = groupResult.Value.Items
			.Select(entry => new ComboBoxItemViewModel(entry.Key, entry.Value))
			.OrderBy(item => item.Id)
			.ToList();

		_groupItemsByGroupName[groupName] = items;

		return items;
	}
}
