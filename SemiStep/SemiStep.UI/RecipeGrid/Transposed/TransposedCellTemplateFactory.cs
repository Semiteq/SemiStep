using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SemiStep.UI.RecipeGrid.Transposed;

internal sealed class TransposedCellTemplateFactory(
	TransposedRecipeGridSurface surface,
	CellEditCoordinator editCoordinator)
{
	internal static readonly FuncValueConverter<bool, bool> NegateConverter = new(value => !value);
	private static readonly PropertyTimeMultiConverter _displayConverter = new();

	// All step columns share one width, so every number cell shares the widest unit slot of the grid.
	private readonly IReadOnlySet<string> _numericKeys = surface.WidthCalculator.NumericColumnKeys();
	private readonly double _unitSlotWidth = surface.WidthCalculator.WidestUnitSlotWidth();
	private readonly TransposedTextEditorFactory _textEditorFactory =
		new(surface, surface.WidthCalculator.WidestUnitSlotWidth());

	public Control CreateEditor(ParameterCellViewModel cell)
	{
		return cell switch
		{
			ComboBoxCellViewModel => CreateComboCellPresenter(cell.Descriptor),
			ReadOnlyCellViewModel readOnlyCell => CreateReadOnlyDisplay(readOnlyCell),
			_ => CreateTextCellPresenter(cell.Descriptor),
		};
	}

	private Control CreateComboCellPresenter(ParameterDescriptor descriptor)
	{
		var presenter = new LazyComboCellPresenter(
			editCoordinator,
			CreateComboDisplay(descriptor),
			CreateComboBox,
			cell => cell.FindAncestorOfType<ListBoxItem>());
		presenter.Bind(InputElement.IsHitTestVisibleProperty, CreateInteractiveBinding());
		presenter.Bind(InputElement.FocusableProperty, CreateInteractiveBinding());
		presenter.Bind(InputElement.IsEnabledProperty, CreateEditableBinding());

		return presenter;
	}

	private Control CreateComboDisplay(ParameterDescriptor descriptor)
	{
		var textBlock = CreateDisplayTextBlock(stretch: true);
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;

		textBlock.Bind(TextBlock.TextProperty, new MultiBinding
		{
			Mode = BindingMode.OneWay,
			Converter = ComboBoxDisplayTextConverter.Instance,
			Bindings =
			{
				new Binding(nameof(ParameterCellViewModel.Value)) { Mode = BindingMode.OneWay },
				new Binding(nameof(ComboBoxCellViewModel.Items)) { Mode = BindingMode.OneWay },
			},
		});

		// The step column stops at its width cap, so an action name may trim; the tooltip always holds it whole.
		if (descriptor.ParameterKey == ColumnTypes.Action)
		{
			textBlock.Bind(ToolTip.TipProperty, new Binding(nameof(TextBlock.Text))
			{
				RelativeSource = new RelativeSource(RelativeSourceMode.Self),
			});
		}

		var loopPassTextPath = descriptor.ParameterKey == ColumnTypes.Action
			? $"{nameof(ParameterCellViewModel.Row)}.{nameof(RecipeRowViewModel.LoopPassText)}"
			: null;

		return ComboBoxCellFactory.CreateDisplayGrid(textBlock, loopPassTextPath);
	}

	private Control CreateTextCellPresenter(ParameterDescriptor descriptor)
	{
		var isNumeric = _numericKeys.Contains(descriptor.ParameterKey);
		var presenter = new TransposedTextCellPresenter(
			editCoordinator,
			CreateValueDisplay(descriptor, PropertyTextEditingMultiConverter.Instance),
			() => _textEditorFactory.Create(isNumeric));
		presenter.Bind(InputElement.IsEnabledProperty, _textEditorFactory.CreateEditableBinding());

		return presenter;
	}

	private Control CreateValueDisplay(ParameterDescriptor descriptor, IMultiValueConverter valueConverter)
	{
		var formatKind = new Binding(nameof(ParameterCellViewModel.FormatKind)) { Mode = BindingMode.OneWay };
		var display = TextCellLayout.Create(
			surface.GridStyle,
			_numericKeys.Contains(descriptor.ParameterKey),
			_unitSlotWidth,
			new MultiBinding
			{
				Mode = BindingMode.OneWay,
				Converter = valueConverter,
				Bindings =
				{
					new Binding(nameof(ParameterCellViewModel.Value)) { Mode = BindingMode.OneWay },
					formatKind,
				},
			},
			new MultiBinding
			{
				Mode = BindingMode.OneWay,
				Converter = TextCellLayout.UnitTextConverter,
				Bindings =
				{
					new Binding(nameof(ParameterCellViewModel.Units)) { Mode = BindingMode.OneWay },
					formatKind,
				},
			});
		display.Bind(Visual.IsVisibleProperty, new Binding(nameof(ParameterCellViewModel.IsApplicable)));

		return display;
	}

	private TextBlock CreateDisplayTextBlock(bool stretch)
	{
		var gridStyle = surface.GridStyle;
		var textBlock = new TextBlock
		{
			VerticalAlignment = VerticalAlignment.Center,
			Padding = new Thickness(
				gridStyle.Layout.CellPaddingLeft,
				gridStyle.Layout.CellPaddingTop,
				gridStyle.Layout.CellPaddingRight,
				gridStyle.Layout.CellPaddingBottom),
			TextAlignment = TextAlignment.Center,
		};
		if (stretch)
		{
			textBlock.HorizontalAlignment = HorizontalAlignment.Stretch;
		}

		GridFontApplier.ApplyCellFont(textBlock, gridStyle);

		return textBlock;
	}

	internal static void CommitByDefocusing(Control editor)
	{
		if (editor.FindAncestorOfType<ListBox>() is { } listBox && listBox.Focus())
		{
			return;
		}

		TopLevel.GetTopLevel(editor)?.FocusManager?.Focus(null);
	}

	// Semi ComboBox selection box does not inherit the grid font, so ApplyCellFont is applied explicitly.
	private ComboBox CreateComboBox()
	{
		var itemsPath = nameof(ComboBoxCellViewModel.Items);
		var comboBox = new ComboBox
		{
			DisplayMemberBinding = new Binding(nameof(ComboBoxItemViewModel.DisplayText)),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Center,
			MinHeight = 0,
		};
		GridFontApplier.ApplyCellFont(comboBox, surface.GridStyle);

		comboBox.Bind(ItemsControl.ItemsSourceProperty, new Binding(itemsPath));

		comboBox.Bind(
			SelectingItemsControl.SelectedItemProperty,
			new MultiBinding
			{
				Mode = BindingMode.OneWay,
				Converter = ComboBoxItemMultiSelectionConverter.Instance,
				Bindings =
				{
					new Binding(nameof(ParameterCellViewModel.Value)),
					new Binding(itemsPath),
				},
			});

		comboBox.SelectionChanged += OnComboBoxSelectionChanged;

		ApplyInputBlocking(comboBox);

		return comboBox;
	}

	// The initial SelectionChanged on a lazy-built combo assigns the value it already holds; Value's setter
	// ignores an unchanged value (RecipeRowViewModel.SetPropertyValue no-ops), so it does not dirty the recipe.
	private static void OnComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (sender is not ComboBox comboBox
			|| comboBox.DataContext is not ParameterCellViewModel cell
			|| comboBox.SelectedItem is not ComboBoxItemViewModel selected)
		{
			return;
		}

		cell.Value = selected.Id.ToString(CultureInfo.InvariantCulture);
	}

	// Step start time arrives pre-formatted from the surface refresh, so it binds directly, right-aligned.
	private Control CreateReadOnlyDisplay(ReadOnlyCellViewModel cell)
	{
		if (ColumnTypes.IsStepStartTimeColumn(cell.Descriptor.ColumnType))
		{
			return TextCellLayout.Create(
				surface.GridStyle,
				isNumeric: true,
				unitSlotWidth: 0,
				new Binding(nameof(ParameterCellViewModel.Value)) { Mode = BindingMode.OneWay },
				unitText: null);
		}

		return CreateValueDisplay(cell.Descriptor, _displayConverter);
	}

	private void ApplyInputBlocking(ComboBox comboBox)
	{
		comboBox.Bind(InputElement.IsHitTestVisibleProperty, CreateInteractiveBinding());
		comboBox.Bind(InputElement.FocusableProperty, CreateInteractiveBinding());
		comboBox.Bind(InputElement.IsEnabledProperty, CreateEditableBinding());
	}

	private static MultiBinding CreateInteractiveBinding()
	{
		return new MultiBinding
		{
			Converter = BoolConverters.And,
			Bindings =
			{
				new Binding(nameof(ParameterCellViewModel.IsApplicable)),
				CreateNotReadOnlyParameterBinding(),
			},
		};
	}

	private MultiBinding CreateEditableBinding()
	{
		return new MultiBinding
		{
			Converter = BoolConverters.And,
			Bindings =
			{
				new Binding(nameof(ParameterCellViewModel.IsApplicable)),
				CreateNotReadOnlyParameterBinding(),
				CreateSurfaceNotReadOnlyBinding(surface),
			},
		};
	}

	internal static Binding CreateSurfaceNotReadOnlyBinding(TransposedRecipeGridSurface surface)
	{
		return new Binding(nameof(TransposedRecipeGridSurface.IsReadOnly))
		{
			Source = surface,
			Converter = NegateConverter,
		};
	}

	private static Binding CreateNotReadOnlyParameterBinding()
	{
		var descriptorReadOnlyPath =
			$"{nameof(ParameterCellViewModel.Descriptor)}.{nameof(ParameterDescriptor.IsReadOnlyParameter)}";

		return new Binding(descriptorReadOnlyPath) { Converter = NegateConverter };
	}
}
