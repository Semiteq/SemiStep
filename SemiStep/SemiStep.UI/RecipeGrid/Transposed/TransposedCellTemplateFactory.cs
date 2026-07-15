using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid.Transposed;

internal sealed class TransposedCellTemplateFactory(
	TransposedRecipeGridSurface surface,
	TransposedTextEditCoordinator editCoordinator)
{
	internal static readonly FuncValueConverter<bool, bool> NegateConverter = new(value => !value);
	private static readonly FuncValueConverter<int?, int> _maxLengthConverter = new(maxLength => maxLength ?? 0);
	private static readonly PropertyTimeMultiConverter _displayConverter = new();

	// Per-editor edit target captured on GotFocus. The commit writes ONLY to this cell, so a
	// still-focused TextBox recycled onto a different cell cannot push its pending text into the new
	// cell; the OneWay display binding meanwhile keeps the new cell's rendered text intact.
	private static readonly AttachedProperty<PropertyTextCellViewModel?> _editingCellProperty =
		AvaloniaProperty.RegisterAttached<TransposedCellTemplateFactory, TextBox, PropertyTextCellViewModel?>(
			"EditingCell");

	public Control CreateEditor(ParameterCellViewModel cell)
	{
		return cell switch
		{
			ComboBoxCellViewModel => CreateComboCellPresenter(),
			ReadOnlyCellViewModel readOnlyCell => CreateReadOnlyTextBlock(readOnlyCell),
			_ => CreateTextCellPresenter(),
		};
	}

	private Control CreateComboCellPresenter()
	{
		var presenter = new TransposedComboCellPresenter(
			editCoordinator,
			CreateComboDisplay(),
			CreateComboBox);
		presenter.Bind(InputElement.IsHitTestVisibleProperty, CreateInteractiveBinding());
		presenter.Bind(InputElement.FocusableProperty, CreateInteractiveBinding());
		presenter.Bind(InputElement.IsEnabledProperty, CreateEditableBinding());

		return presenter;
	}

	private Control CreateComboDisplay()
	{
		var textBlock = CreateDisplayTextBlock(stretch: true);

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

		return textBlock;
	}

	private Control CreateTextCellPresenter()
	{
		var presenter = new TransposedTextCellPresenter(
			editCoordinator,
			CreateTextDisplay(),
			CreateTextBoxEditor);
		presenter.Bind(InputElement.IsEnabledProperty, CreateTextBoxEditableBinding());

		return presenter;
	}

	private Control CreateTextDisplay()
	{
		var textBlock = CreateDisplayTextBlock(stretch: true);

		textBlock.Bind(TextBlock.TextProperty, new MultiBinding
		{
			Mode = BindingMode.OneWay,
			Converter = PropertyTextEditingMultiConverter.Instance,
			Bindings =
			{
				new Binding(nameof(ParameterCellViewModel.Value)) { Mode = BindingMode.OneWay },
				new Binding(nameof(ParameterCellViewModel.FormatKind)) { Mode = BindingMode.OneWay },
			},
		});

		return textBlock;
	}

	private TextBlock CreateDisplayTextBlock(bool stretch)
	{
		var gridStyle = surface.GridStyle;
		var textBlock = new TextBlock
		{
			VerticalAlignment = VerticalAlignment.Center,
			Padding = new Thickness(
				gridStyle.CellPaddingLeft,
				gridStyle.CellPaddingTop,
				gridStyle.CellPaddingRight,
				gridStyle.CellPaddingBottom),
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

	private Control CreateTextBoxEditor()
	{
		var gridStyle = surface.GridStyle;

		var textBox = new TextBox
		{
			VerticalAlignment = VerticalAlignment.Center,
			VerticalContentAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			// The Semi TextBox theme floors MinHeight at 32; size to the configured row instead.
			MinHeight = 0,
			BorderThickness = new Thickness(0),
			Background = Brushes.Transparent,
			Padding = new Thickness(
				gridStyle.CellPaddingLeft,
				gridStyle.CellPaddingTop,
				gridStyle.CellPaddingRight,
				gridStyle.CellPaddingBottom),
			TextAlignment = TextAlignment.Center,
		};
		GridFontApplier.ApplyCellFont(textBox, gridStyle);

		// null MaxLength maps to 0 (Semi's "unlimited").
		textBox.Bind(TextBox.MaxLengthProperty, new Binding(nameof(PropertyTextCellViewModel.MaxLength))
		{
			Mode = BindingMode.OneWay,
			Converter = _maxLengthConverter,
		});

		// OneWay only — a MultiBinding cannot ConvertBack; commit is owned by the handlers below.
		textBox.Bind(TextBox.TextProperty, new MultiBinding
		{
			Mode = BindingMode.OneWay,
			Converter = PropertyTextEditingMultiConverter.Instance,
			Bindings =
			{
				new Binding(nameof(ParameterCellViewModel.Value)) { Mode = BindingMode.OneWay },
				new Binding(nameof(ParameterCellViewModel.FormatKind)) { Mode = BindingMode.OneWay },
			},
		});

		// A TextBox cell is never column-level read-only (read-only descriptors route to ReadOnlyCellViewModel), so no read-only leg.
		textBox.Bind(InputElement.IsEnabledProperty, CreateTextBoxEditableBinding());

		textBox.GotFocus += (sender, _) => OnEditorGotFocus(sender);
		textBox.LostFocus += (sender, _) => OnEditorLostFocus(sender);
		textBox.AddHandler(
			InputElement.KeyDownEvent,
			OnEditorKeyDown,
			RoutingStrategies.Bubble,
			handledEventsToo: true);

		return textBox;
	}

	// Stale-guard capture: the cell being edited is pinned when the TextBox gains focus, so the
	// commit targets it even if recycling later rebinds the control onto a different cell.
	private static void OnEditorGotFocus(object? sender)
	{
		if (sender is TextBox textBox)
		{
			textBox.SetValue(_editingCellProperty, textBox.DataContext as PropertyTextCellViewModel);
		}
	}

	private static void OnEditorLostFocus(object? sender)
	{
		if (sender is TextBox textBox)
		{
			CommitEditor(textBox);
		}
	}

	// Enter commits by defocusing; Escape reverts the TextBox to the captured value first so the ensuing commit re-parses to a no-op.
	private static void OnEditorKeyDown(object? sender, KeyEventArgs e)
	{
		if (sender is not TextBox textBox || e.Key is not (Key.Enter or Key.Escape))
		{
			return;
		}

		if (e.Key == Key.Escape)
		{
			var cell = textBox.GetValue(_editingCellProperty);
			textBox.Text = PropertyTimeEditingConverter.FormatForDisplay(cell?.Value, cell?.FormatKind);
		}

		CommitByDefocusing(textBox);
		e.Handled = true;
	}

	// Commit writes only to the captured cell; snap the display back only while the editor still shows it, else it was recycled onto another cell.
	internal static void CommitEditor(TextBox textBox)
	{
		if (textBox.GetValue(_editingCellProperty) is not { } cell)
		{
			return;
		}

		var parsed = PropertyTimeEditingConverter.ParseForCommit(textBox.Text, cell.MaxLength.HasValue);
		if (!ReferenceEquals(parsed, BindingOperations.DoNothing))
		{
			cell.Value = parsed;
		}

		if (ReferenceEquals(textBox.DataContext, cell))
		{
			textBox.Text = PropertyTimeEditingConverter.FormatForDisplay(cell.Value, cell.FormatKind);
		}
	}

	private Control CreateReadOnlyTextBlock(ReadOnlyCellViewModel cell)
	{
		var textBlock = CreateDisplayTextBlock(stretch: false);

		// Step start time arrives pre-formatted from the surface refresh, so it binds directly.
		if (ColumnTypes.IsStepStartTimeColumn(cell.Descriptor.ColumnType))
		{
			textBlock.Bind(TextBlock.TextProperty, new Binding(nameof(ParameterCellViewModel.Value))
			{
				Mode = BindingMode.OneWay,
			});

			return textBlock;
		}

		textBlock.Bind(TextBlock.TextProperty, new MultiBinding
		{
			Converter = _displayConverter,
			Bindings =
			{
				new Binding(nameof(ParameterCellViewModel.Value)) { Mode = BindingMode.OneWay },
				new Binding(nameof(ParameterCellViewModel.Units)) { Mode = BindingMode.OneWay },
				new Binding(nameof(ParameterCellViewModel.FormatKind)) { Mode = BindingMode.OneWay },
			},
		});

		return textBlock;
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
				CreateSurfaceNotReadOnlyBinding(),
			},
		};
	}

	private MultiBinding CreateTextBoxEditableBinding()
	{
		return new MultiBinding
		{
			Converter = BoolConverters.And,
			Bindings =
			{
				new Binding(nameof(ParameterCellViewModel.IsApplicable)),
				CreateSurfaceNotReadOnlyBinding(),
			},
		};
	}

	private Binding CreateSurfaceNotReadOnlyBinding()
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
