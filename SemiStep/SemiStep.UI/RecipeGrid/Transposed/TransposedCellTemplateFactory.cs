using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
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
/// Builds the per-kind cell editor templates for the transposed grid, mirroring the canonical
/// TextCellFactory / ComboBoxCellFactory: always-live editors dispatched over the cell-VM type,
/// display and parse-back through the shared time/unit converters, input gated on applicability,
/// column-level read-only, and the surface read-only state.
/// </summary>
internal sealed class TransposedCellTemplateFactory(TransposedRecipeGridSurface surface)
{
	private static readonly FuncValueConverter<bool, bool> _negateConverter = new(value => !value);
	private static readonly PropertyTimeMultiConverter _displayConverter = new();

	public IReadOnlyList<IDataTemplate> CreateTemplates()
	{
		return
		[
			CreateComboBoxTemplate(),
			CreatePropertyTextTemplate(),
			CreateReadOnlyTemplate(),
		];
	}

	internal static void CommitByDefocusing(Control editor)
	{
		if (editor.FindAncestorOfType<ListBox>() is { } listBox && listBox.Focus())
		{
			return;
		}

		TopLevel.GetTopLevel(editor)?.FocusManager?.Focus(null);
	}

	private IDataTemplate CreateComboBoxTemplate()
	{
		return new FuncDataTemplate<ComboBoxCellViewModel>((_, _) => CreateComboBox(), supportsRecycling: true);
	}

	// FontSize explicit: the Semi ComboBox selection box does not inherit the grid font (parity
	// with the canonical ComboBoxCellFactory). SelectedItem resolves via a OneWay MultiBinding;
	// writeback is owned by SelectionChanged, which no-ops when the value is unchanged.
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

	private IDataTemplate CreatePropertyTextTemplate()
	{
		return new FuncDataTemplate<PropertyTextCellViewModel>(
			(cell, _) => cell is null ? new TextBlock() : CreateTextBoxEditor(cell),
			supportsRecycling: false);
	}

	private Control CreateTextBoxEditor(PropertyTextCellViewModel cell)
	{
		var gridStyle = surface.GridStyle;
		var editingConverter = new PropertyTimeEditingConverter(cell.FormatKind, allowsEmpty: cell.MaxLength.HasValue);

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

		if (cell.MaxLength.HasValue)
		{
			textBox.MaxLength = cell.MaxLength.Value;
		}

		textBox.Bind(TextBox.TextProperty, new Binding(nameof(ParameterCellViewModel.Value))
		{
			Mode = BindingMode.TwoWay,
			UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
			Converter = editingConverter,
		});

		// Subscribed after Bind so it runs once the LostFocus push has resolved: snap the display
		// back to the view model. A rejected, read-only-dropped, or unparseable write leaves the
		// source unchanged, and the binding's publish cache skips re-publishing an identical
		// value, so the editor would otherwise keep showing the never-committed text.
		textBox.LostFocus += (sender, _) =>
		{
			if (sender is TextBox senderTextBox)
			{
				senderTextBox.Text = editingConverter.Convert(
					cell.Value, typeof(string), null, CultureInfo.CurrentCulture) as string;
			}
		};

		// No read-only-parameter leg here: the cell factory routes read-only descriptors to
		// ReadOnlyCellViewModel before the property-text kind, so a TextBox cell is never
		// column-level read-only.
		textBox.Bind(InputElement.IsEnabledProperty, CreateTextBoxEditableBinding());

		textBox.AddHandler(
			InputElement.KeyDownEvent,
			(sender, e) => OnEditorKeyDown(sender, e, cell, editingConverter),
			RoutingStrategies.Bubble,
			handledEventsToo: true);

		return textBox;
	}

	// Enter commits an always-live editor by moving focus off it; the LostFocus trigger pushes
	// the pending text through the editing converter (canonical parity: Enter ends the cell
	// edit). Escape overwrites the typed-but-uncommitted text with the view-model value before
	// defocusing (canonical parity: Escape cancels the cell edit) — written directly to Text
	// because the two-way binding withholds source-to-target updates while an edit is pending.
	private static void OnEditorKeyDown(
		object? sender,
		KeyEventArgs e,
		PropertyTextCellViewModel cell,
		PropertyTimeEditingConverter editingConverter)
	{
		if (sender is not TextBox textBox || e.Key is not (Key.Enter or Key.Escape))
		{
			return;
		}

		if (e.Key == Key.Escape)
		{
			textBox.Text = editingConverter.Convert(
				cell.Value, typeof(string), null, CultureInfo.CurrentCulture) as string;
		}

		CommitByDefocusing(textBox);
		e.Handled = true;
	}

	private IDataTemplate CreateReadOnlyTemplate()
	{
		return new FuncDataTemplate<ReadOnlyCellViewModel>(
			(cell, _) => cell is null ? new TextBlock() : CreateReadOnlyTextBlock(cell),
			supportsRecycling: false);
	}

	private Control CreateReadOnlyTextBlock(ReadOnlyCellViewModel cell)
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
		GridFontApplier.ApplyCellFont(textBlock, gridStyle);

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
			Converter = _negateConverter,
		};
	}

	private static Binding CreateNotReadOnlyParameterBinding()
	{
		var descriptorReadOnlyPath =
			$"{nameof(ParameterCellViewModel.Descriptor)}.{nameof(ParameterDescriptor.IsReadOnlyParameter)}";

		return new Binding(descriptorReadOnlyPath) { Converter = _negateConverter };
	}
}
