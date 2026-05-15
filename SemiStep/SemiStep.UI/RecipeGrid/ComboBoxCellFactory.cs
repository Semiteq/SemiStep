using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
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
		var applicabilityConverter = CreateApplicabilityConverter(ColumnTypes.Action);
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

			ApplyDisabledState(comboBox, applicabilityConverter, isColumnReadOnly);

			return comboBox;
		}, supportsRecycling: true);
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateGroupCellTemplate(string columnKey, bool isColumnReadOnly)
	{
		var applicabilityConverter = CreateApplicabilityConverter(columnKey);
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

			ApplyDisabledState(comboBox, applicabilityConverter, isColumnReadOnly);

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

	private static FuncValueConverter<IReadOnlySet<string>?, bool> CreateApplicabilityConverter(string columnKey)
	{
		return new FuncValueConverter<IReadOnlySet<string>?, bool>(
			set => set is null || !set.Contains(columnKey));
	}

	private static void ApplyDisabledState(
		ComboBox comboBox,
		IValueConverter applicabilityConverter,
		bool isColumnReadOnly)
	{
		if (isColumnReadOnly)
		{
			comboBox.IsHitTestVisible = false;
			comboBox.Focusable = false;
			return;
		}

		comboBox.Bind(InputElement.IsHitTestVisibleProperty, new Binding(nameof(RecipeRowViewModel.InapplicableColumns))
		{
			Converter = applicabilityConverter,
			Mode = BindingMode.OneWay,
		});

		comboBox.Bind(InputElement.FocusableProperty, new Binding(nameof(RecipeRowViewModel.InapplicableColumns))
		{
			Converter = applicabilityConverter,
			Mode = BindingMode.OneWay,
		});
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
