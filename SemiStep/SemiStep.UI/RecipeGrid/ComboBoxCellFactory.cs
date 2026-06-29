using System.Globalization;

using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

internal sealed class ComboBoxCellFactory(RecipeMetadataRegistry recipeMetadataRegistry, GridStyleOptions gridStyle)
{
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
		var items = recipeMetadataRegistry.GetActionComboBoxItems();
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

				row.SetPropertyValue(columnKey, selected.Id.ToString(CultureInfo.InvariantCulture));
			};

			ApplyInputBlocking(comboBox, columnKey, isColumnReadOnly);

			return comboBox;
		}, supportsRecycling: true);
	}

	// FontSize explicit: the Fluent ComboBox selection box does not inherit the grid font, so without it
	// the text renders at the theme default and the chevron clips it. See Docs/architecture/recipe-grid-column-sizing.md.
	private ComboBox CreateStyledComboBox()
	{
		var comboBox = new ComboBox
		{
			DisplayMemberBinding = new Binding(nameof(ComboBoxItemViewModel.DisplayText)),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Center,
		};
		GridFontApplier.ApplyCellFont(comboBox, gridStyle);

		return comboBox;
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
}
