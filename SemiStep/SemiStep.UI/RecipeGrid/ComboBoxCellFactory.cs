using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;

using Path = Avalonia.Controls.Shapes.Path;

namespace SemiStep.UI.RecipeGrid;

/// <summary>Builds the canonical combo columns: a text display cell whose ComboBox exists only while edited.</summary>
internal sealed class ComboBoxCellFactory(ComboBoxItemsProvider comboBoxItemsProvider, GridStyleOptions gridStyle)
{
	internal const string ChevronClass = "cell-chevron";

	// Matches the left padding of the ComboBox selection box, so the text does not move when the display swaps to
	// the editor. The width calculator reserves it with the chevron slot.
	internal const double DisplayPaddingLeft = 6;
	internal const double ChevronSlotWidth = 32;

	public DataGridColumn CreateActionColumn(
		GridColumnDefinition columnDef,
		DataGridLength width,
		CellEditCoordinator editCoordinator)
	{
		var items = comboBoxItemsProvider.ActionItems;
		var selectionConverter = new ComboBoxItemSelectionConverter(items);

		return CreateColumn(columnDef, width, () => new FuncDataTemplate<RecipeRowViewModel>(
			(_, _) => CreatePresenter(
				columnDef,
				editCoordinator,
				CreateDisplayTextBinding(
					new Binding(ColumnTypes.IndexerPath(ColumnTypes.Action)),
					new Binding { Source = items }),
				() => CreateActionComboBox(items, selectionConverter),
				nameof(RecipeRowViewModel.LoopPassText)),
			supportsRecycling: true));
	}

	public DataGridColumn CreateGroupComboBoxColumn(
		GridColumnDefinition columnDef,
		DataGridLength width,
		CellEditCoordinator editCoordinator)
	{
		return CreateColumn(columnDef, width, () => new FuncDataTemplate<RecipeRowViewModel>(
			(_, _) => CreatePresenter(
				columnDef,
				editCoordinator,
				CreateDisplayTextBinding(
					new Binding(ColumnTypes.IndexerPath(columnDef.Key)),
					new Binding(ColumnTypes.GroupItemsPath(columnDef.Key))),
				() => CreateGroupComboBox(columnDef.Key),
				loopPassTextPath: null),
			supportsRecycling: true));
	}

	/// <summary>
	/// The display of a combo cell in either grid: the trimming name, the loop pill if any, the chevron slot.
	/// </summary>
	internal static Grid CreateDisplayGrid(TextBlock name, string? loopPassTextPath)
	{
		var chevron = new Path
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			IsHitTestVisible = false,
		};
		chevron.Classes.Add(ChevronClass);
		Grid.SetColumn(chevron, 2);

		var grid = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Auto),
				new ColumnDefinition(ChevronSlotWidth, GridUnitType.Pixel),
			},
			Background = Brushes.Transparent,
			Children = { name, chevron },
		};

		if (loopPassTextPath is not null)
		{
			var pill = LoopBadge.Create(loopPassTextPath);
			Grid.SetColumn(pill, 1);
			grid.Children.Add(pill);
		}

		return grid;
	}

	private static DataGridTemplateColumn CreateColumn(
		GridColumnDefinition columnDef,
		DataGridLength width,
		Func<IDataTemplate> cellTemplate)
	{
		return new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Tag = columnDef.Key,
			Width = width,
			IsReadOnly = true,
			CanUserSort = false,
			CellTemplate = cellTemplate(),
		};
	}

	private LazyComboCellPresenter CreatePresenter(
		GridColumnDefinition columnDef,
		CellEditCoordinator editCoordinator,
		MultiBinding displayTextBinding,
		Func<ComboBox> editorBuilder,
		string? loopPassTextPath)
	{
		var presenter = new LazyComboCellPresenter(
			editCoordinator,
			CreateDisplay(displayTextBinding, loopPassTextPath),
			editorBuilder,
			cell => cell.FindAncestorOfType<DataGrid>());

		if (columnDef.ReadOnly)
		{
			presenter.IsEnabled = false;
		}
		else
		{
			presenter.Bind(
				InputElement.IsEnabledProperty,
				CellApplicabilityBinding.CreateApplicableAndUnlockedBinding(columnDef.Key));
		}

		return presenter;
	}

	private static MultiBinding CreateDisplayTextBinding(Binding idBinding, Binding itemsBinding)
	{
		return new MultiBinding
		{
			Mode = BindingMode.OneWay,
			Converter = ComboBoxDisplayTextConverter.Instance,
			Bindings = { idBinding, itemsBinding },
		};
	}

	private Grid CreateDisplay(MultiBinding displayTextBinding, string? loopPassTextPath)
	{
		var text = new TextBlock
		{
			VerticalAlignment = VerticalAlignment.Center,
			Padding = new Thickness(DisplayPaddingLeft, 0, 0, 0),
			TextTrimming = TextTrimming.CharacterEllipsis,
		};
		GridFontApplier.ApplyCellFont(text, gridStyle);
		text.Bind(TextBlock.TextProperty, displayTextBinding);

		return CreateDisplayGrid(text, loopPassTextPath);
	}

	private ComboBox CreateActionComboBox(
		IReadOnlyList<ComboBoxItemViewModel> items,
		ComboBoxItemSelectionConverter selectionConverter)
	{
		var comboBox = CreateStyledComboBox(ColumnTypes.Action);
		comboBox.ItemsSource = items;
		comboBox.Bind(
			SelectingItemsControl.SelectedItemProperty,
			new Binding(ColumnTypes.IndexerPath(ColumnTypes.Action))
			{
				Mode = BindingMode.OneWay,
				Converter = selectionConverter,
			});

		return comboBox;
	}

	// ItemsSource follows the row VM; SelectedItem resolves through a MultiBinding that waits for both legs.
	private ComboBox CreateGroupComboBox(string columnKey)
	{
		var comboBox = CreateStyledComboBox(columnKey);
		comboBox.Bind(ItemsControl.ItemsSourceProperty, new Binding(ColumnTypes.GroupItemsPath(columnKey)));
		comboBox.Bind(SelectingItemsControl.SelectedItemProperty, CreateGroupSelectionBinding(columnKey));

		return comboBox;
	}

	private static MultiBinding CreateGroupSelectionBinding(string columnKey)
	{
		return new MultiBinding
		{
			Mode = BindingMode.OneWay,
			Converter = ComboBoxItemMultiSelectionConverter.Instance,
			Bindings =
			{
				new Binding(ColumnTypes.IndexerPath(columnKey)),
				new Binding(ColumnTypes.GroupItemsPath(columnKey)),
			},
		};
	}

	// FontSize explicit: the Semi ComboBox selection box does not inherit the grid font, so without it
	// the text renders at the theme default and the chevron clips it. See docs/architecture/recipe-grid-column-sizing.md.
	// The write-back is the pick only: the binding is OneWay, and a detached or re-entered editor selects the
	// value its row already holds, which the row ignores.
	private ComboBox CreateStyledComboBox(string columnKey)
	{
		var comboBox = new ComboBox
		{
			DisplayMemberBinding = new Binding(nameof(ComboBoxItemViewModel.DisplayText)),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Center,
			MinHeight = 0,
		};
		GridFontApplier.ApplyCellFont(comboBox, gridStyle);

		comboBox.SelectionChanged += (_, _) =>
		{
			if (comboBox.DataContext is RecipeRowViewModel row
				&& comboBox.SelectedItem is ComboBoxItemViewModel selected)
			{
				row.SetPropertyValue(columnKey, selected.Id.ToString(CultureInfo.InvariantCulture));
			}
		};

		return comboBox;
	}
}
