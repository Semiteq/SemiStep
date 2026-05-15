using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

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
		var inapplicableColumnsConverter = new InapplicableColumnsConverter(ColumnTypes.Action);
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

			ApplyDisabledClass(comboBox, inapplicableColumnsConverter, isColumnReadOnly);

			return comboBox;
		}, supportsRecycling: true);
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateGroupCellTemplate(string columnKey, bool isColumnReadOnly)
	{
		var inapplicableColumnsConverter = new InapplicableColumnsConverter(columnKey);
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

			ApplyDisabledClass(comboBox, inapplicableColumnsConverter, isColumnReadOnly);

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
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0),
		};
	}

	private static void ApplyDisabledClass(
		ComboBox comboBox,
		InapplicableColumnsConverter inapplicableColumnsConverter,
		bool isColumnReadOnly)
	{
		if (isColumnReadOnly)
		{
			comboBox.Classes.Set("disabled", true);
			return;
		}

		var disabledBinding = DisabledClassBinding.Create(inapplicableColumnsConverter);
		comboBox.BindClass("disabled", disabledBinding, comboBox);
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
			.Select(kvp => new ComboBoxItemViewModel(kvp.Key, kvp.Value))
			.OrderBy(item => item.Id)
			.ToList();

		_groupItemsByGroupName[groupName] = items;

		return items;
	}
}
