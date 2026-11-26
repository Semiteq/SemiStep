using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SemiStep.UI.RecipeGrid.Transposed;

/// <summary>Builds the TextBox editor of a transposed text cell and owns its commit to the edited cell.</summary>
internal sealed class TransposedTextEditorFactory(TransposedRecipeGridSurface surface, double unitSlotWidth)
{
	private static readonly FuncValueConverter<int?, int> _maxLengthConverter = new(maxLength => maxLength ?? 0);

	// Per-editor edit target captured on GotFocus. The commit writes ONLY to this cell, so a
	// still-focused TextBox recycled onto a different cell cannot push its pending text into the new
	// cell; the OneWay display binding meanwhile keeps the new cell's rendered text intact.
	private static readonly AttachedProperty<PropertyTextCellViewModel?> _editingCellProperty =
		AvaloniaProperty.RegisterAttached<TransposedTextEditorFactory, TextBox, PropertyTextCellViewModel?>(
			"EditingCell");

	/// <summary>A text cell takes input while it applies and the grid is not read-only.</summary>
	public MultiBinding CreateEditableBinding()
	{
		return new MultiBinding
		{
			Converter = BoolConverters.And,
			Bindings =
			{
				new Binding(nameof(ParameterCellViewModel.IsApplicable)),
				TransposedCellTemplateFactory.CreateSurfaceNotReadOnlyBinding(surface),
			},
		};
	}

	public Control Create(bool isNumeric)
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
			Padding = TextCellLayout.EditorPadding(gridStyle.Layout, isNumeric, unitSlotWidth),
			TextAlignment = isNumeric ? TextAlignment.Right : TextAlignment.Left,
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

		// A TextBox cell is never column-level read-only (read-only descriptors route to
		// ReadOnlyCellViewModel), so no read-only leg.
		textBox.Bind(InputElement.IsEnabledProperty, CreateEditableBinding());

		textBox.GotFocus += (sender, _) => OnEditorGotFocus(sender);
		textBox.LostFocus += (sender, _) => OnEditorLostFocus(sender);
		textBox.AddHandler(
			InputElement.KeyDownEvent,
			OnEditorKeyDown,
			RoutingStrategies.Bubble,
			handledEventsToo: true);

		return textBox;
	}

	// Commit writes only to the captured cell; snap the display back only while the editor still shows it, else it
	// was recycled onto another cell.
	internal static void CommitEditor(TextBox textBox)
	{
		if (textBox.GetValue(_editingCellProperty) is not { } cell)
		{
			return;
		}

		var parsed = PropertyTimeEditingConverter.ParseForCommit(
			textBox.Text,
			cell.FormatKind,
			cell.MaxLength.HasValue);
		if (!ReferenceEquals(parsed, BindingOperations.DoNothing))
		{
			cell.Value = parsed;
		}

		if (ReferenceEquals(textBox.DataContext, cell))
		{
			textBox.Text = PropertyTimeEditingConverter.FormatForDisplay(cell.Value, cell.FormatKind);
		}
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

	// Enter commits by defocusing. Escape drops the edit target, so the blur writes nothing, and hands focus to the
	// column container, as the combo Esc does, so the arrow keys keep working.
	private static void OnEditorKeyDown(object? sender, KeyEventArgs e)
	{
		if (sender is not TextBox textBox || e.Key is not (Key.Enter or Key.Escape))
		{
			return;
		}

		e.Handled = true;
		if (e.Key == Key.Enter)
		{
			TransposedCellTemplateFactory.CommitByDefocusing(textBox);
			return;
		}

		var cell = textBox.GetValue(_editingCellProperty);
		textBox.Text = PropertyTimeEditingConverter.FormatForDisplay(cell?.Value, cell?.FormatKind);
		textBox.ClearValue(_editingCellProperty);
		if (textBox.FindAncestorOfType<ListBoxItem>()?.Focus() != true)
		{
			TransposedCellTemplateFactory.CommitByDefocusing(textBox);
		}
	}
}
