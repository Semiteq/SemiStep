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

/// <summary>
/// Builds the per-kind cell controls for the transposed grid, dispatched over the cell-VM type. Both
/// editor kinds are lazy: property-text and ComboBox cells render a lightweight display and build their
/// heavy editor (TextBox / ComboBox) only on edit entry through the view-level edit coordinator, released
/// back to the display on exit. Display and parse-back go through the shared time/unit converters; input
/// is gated on applicability, column-level read-only, and the surface read-only state.
/// </summary>
internal sealed class TransposedCellTemplateFactory(
	TransposedRecipeGridSurface surface,
	TransposedTextEditCoordinator editCoordinator)
{
	// Shared bool negation, reused by the pooled column-cells presenter's class bindings.
	internal static readonly FuncValueConverter<bool, bool> NegateConverter = new(value => !value);
	private static readonly FuncValueConverter<int?, int> _maxLengthConverter = new(maxLength => maxLength ?? 0);
	private static readonly PropertyTimeMultiConverter _displayConverter = new();

	// Per-editor edit target captured on GotFocus. The commit writes ONLY to this cell, so a
	// still-focused TextBox recycled onto a different cell cannot push its pending text into the new
	// cell; the OneWay display binding meanwhile keeps the new cell's rendered text intact.
	private static readonly AttachedProperty<PropertyTextCellViewModel?> _editingCellProperty =
		AvaloniaProperty.RegisterAttached<TransposedCellTemplateFactory, TextBox, PropertyTextCellViewModel?>(
			"EditingCell");

	// Builds the per-kind editor control directly (no ContentControl/ContentPresenter wrapper), so a
	// pooled column presenter can host it as a plain child that survives detach/reattach and only
	// rebinds on DataContext change. The kind is constant per slot (every column's cell at a given row
	// shares one descriptor), so the editor built from the first bound cell stays correct across reuse.
	public Control CreateEditor(ParameterCellViewModel cell)
	{
		return cell switch
		{
			ComboBoxCellViewModel => CreateComboCellPresenter(),
			ReadOnlyCellViewModel readOnlyCell => CreateReadOnlyTextBlock(readOnlyCell),
			_ => CreateTextCellPresenter(),
		};
	}

	// ComboBox cells are lazy: a display TextBlock showing the selected item's text by default, with the
	// heavy ComboBox editor built only when the coordinator enters edit here. Hit-testable/focusable follow
	// applicability + column read-only (a read-only or inapplicable combo is non-hit-testable and
	// non-focusable, so it cannot enter edit and a press falls through to column selection); IsEnabled adds
	// the surface read-only state.
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

		// The same OneWay (Value, Items) lookup the ComboBox editor's SelectedItem uses, projected to the
		// item's display text, so a recycled slot rebinds its display to the new cell's selection and an
		// external value change (selector edit / action change) updates the shown text.
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

	// Property-text cells are lazy: a display TextBlock by default, with the TextBox editor built only
	// when the coordinator enters edit here. The display mirrors the editor's units-less formatting so
	// entering/leaving edit shows identical text; IsEnabled follows applicability + surface read-only so
	// inapplicable / read-only-surface cells are non-hit-testable and non-focusable (edit entry blocked).
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

		// The same OneWay (Value, FormatKind) MultiBinding the editor uses, so a recycled slot rebinds
		// its display to the new cell's value and the display matches what the editor would show.
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

	// The display TextBlock shared by all three cell kinds (combo display, text display, read-only): same
	// vertical centering, cell padding, centered text, and grid font. Callers attach their own Text binding.
	// The two editor-backed kinds pass stretch: true to fill the cell width; the read-only kind keeps the
	// default alignment.
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

	// The ComboBox editor, built lazily by the combo presenter on edit entry. FontSize explicit: the Semi
	// ComboBox selection box does not inherit the grid font (parity with the canonical ComboBoxCellFactory).
	// SelectedItem resolves via a OneWay MultiBinding; writeback is owned by SelectionChanged, which no-ops
	// when the value is unchanged (so the initial selection on a lazy build is a no-op, not a recipe edit).
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

	// The TextBox carries no per-cell baked state: display is a OneWay MultiBinding through a shared
	// stateless converter, MaxLength is bound, and the edit commit reads its target from the cell
	// captured on GotFocus (not a closure), so a pooled editor safely rebinds onto the next cell on a
	// DataContext change instead of being rebuilt.
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

		// Bound, not baked: a null MaxLength maps to 0 (Semi's "unlimited"), matching the old
		// leave-MaxLength-unset behavior while still tracking the recycled cell's descriptor.
		textBox.Bind(TextBox.MaxLengthProperty, new Binding(nameof(PropertyTextCellViewModel.MaxLength))
		{
			Mode = BindingMode.OneWay,
			Converter = _maxLengthConverter,
		});

		// OneWay display only (a MultiBinding cannot ConvertBack). FormatKind is bound so the shared
		// converter formats each recycled cell correctly; the commit is owned by the handlers below.
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

		// No read-only-parameter leg here: the cell factory routes read-only descriptors to
		// ReadOnlyCellViewModel before the property-text kind, so a TextBox cell is never
		// column-level read-only.
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

	// Enter commits an always-live editor by moving focus off it; the LostFocus handler parses and
	// pushes the pending text (canonical parity: Enter ends the cell edit). Escape overwrites the
	// typed-but-uncommitted text with the captured cell's value before defocusing (canonical parity:
	// Escape cancels the cell edit) — the ensuing commit re-parses that reverted text to a no-op write.
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

	// Commit writes ONLY to the cell captured on GotFocus, never the current DataContext, so a
	// recycled-out editor commits its edit to the cell the user was editing and a recycled-in editor
	// never receives a stale write. The display is snapped back to the model value only while the
	// editor still shows the captured cell — a rejected, read-only-dropped, or unchanged write leaves
	// the source untouched, so the OneWay binding would otherwise keep showing the never-committed
	// text; if the editor was recycled onto another cell, its binding already shows that cell and must
	// not be overwritten.
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

	// The read-only TextBlock holds no per-cell baked state: both the step-start-time and the
	// display-converter legs are OneWay bindings. The step-start-time vs display split keys off the
	// descriptor's ColumnType, which is invariant per slot position (every column's cell at a given row
	// shares one parameter descriptor), so the chosen leg stays correct across reuse.
	private Control CreateReadOnlyTextBlock(ReadOnlyCellViewModel cell)
	{
		var textBlock = CreateDisplayTextBlock(stretch: false);

		// Step start time arrives pre-formatted (HH:MM:SS + units) from the surface refresh tail,
		// so it binds directly; other read-only cells format through the shared display converter.
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

	// Canonical ApplyInputBlocking parity: a column-level read-only combo is permanently
	// non-hit-testable and non-focusable; otherwise both follow per-cell applicability.
	// IsEnabled additionally tracks the surface read-only state.
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
