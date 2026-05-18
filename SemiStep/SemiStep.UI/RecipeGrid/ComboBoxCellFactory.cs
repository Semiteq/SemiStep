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
	private List<ComboBoxItemViewModel>? _cachedActionItems;

	public void InvalidateCaches()
	{
		_cachedActionItems = null;
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
		return new FuncDataTemplate<RecipeRowViewModel>((_, _) =>
		{
			var comboBox = CreateStyledComboBox();

			// ItemsSource follows the recycled cell's current row VM. SelectedItem resolves via
			// a OneWay MultiBinding so the lookup waits for both legs before invoking Convert.
			// Writeback is handled by SelectionChanged below.
			comboBox.Bind(
				ItemsControl.ItemsSourceProperty,
				new Binding(ColumnTypes.GroupItemsPath(columnKey)));

			comboBox.Bind(
				ComboBox.SelectedItemProperty,
				new MultiBinding
				{
					Mode = BindingMode.OneWay,
					Converter = ComboBoxItemMultiSelectionConverter.Instance,
					Bindings =
					{
						new Binding(ColumnTypes.IndexerPath(columnKey)),
						new Binding(ColumnTypes.GroupItemsPath(columnKey)),
					},
				});

			comboBox.SelectionChanged += (_, _) =>
			{
				if (comboBox.DataContext is not RecipeRowViewModel row)
				{
					return;
				}

				if (comboBox.SelectedItem is not ComboBoxItemViewModel selected)
				{
					return;
				}

				row.SetPropertyValue(columnKey, selected.Id.ToString());
			};

			ApplyInputBlocking(comboBox, columnKey, isColumnReadOnly);

			return comboBox;
		}, supportsRecycling: true);
	}

	private static ComboBox CreateStyledComboBox()
	{
		return new ComboBox
		{
			DisplayMemberBinding = new Binding(nameof(ComboBoxItemViewModel.DisplayText)),
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
}
