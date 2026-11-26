using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Styling;

namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// Per-column <see cref="ControlTheme"/> for <see cref="DataGridCell"/> that resolves the cell's colour from
/// <see cref="Styles.CellPalette"/> into <see cref="CellBackgroundProperty"/>; a style in DataGridStyles.axaml
/// paints it, so it outranks the Semi theme's own cell backgrounds.
/// </summary>
internal static class RecipeCellTheme
{
	// Transparent, not null, until the palette resolves: a null cell background drops the cell out of hit
	// testing, and a press then no longer reaches the cell that selects the row.
	public static readonly AttachedProperty<IBrush?> CellBackgroundProperty =
		AvaloniaProperty.RegisterAttached<DataGridCell, IBrush?>(
			"CellBackground",
			typeof(RecipeCellTheme),
			defaultValue: Brushes.Transparent);

	public static IBrush? GetCellBackground(DataGridCell cell)
	{
		return cell.GetValue(CellBackgroundProperty);
	}

	public static void SetCellBackground(DataGridCell cell, IBrush? value)
	{
		cell.SetValue(CellBackgroundProperty, value);
	}

	/// <summary>A null <paramref name="columnKey"/> is the numbering column, which is never inapplicable or marked.</summary>
	public static ControlTheme Create(string? columnKey, bool isReadOnly)
	{
		var theme = new ControlTheme(typeof(DataGridCell))
		{
			BasedOn = ResolveDefaultDataGridCellTheme(),
		};

		theme.Add(new Setter(CellBackgroundProperty, CreateBackgroundBinding(columnKey, isReadOnly)));

		return theme;
	}

	private static MultiBinding CreateBackgroundBinding(string? columnKey, bool isReadOnly)
	{
		return new MultiBinding
		{
			Converter = CellBackgroundConverter.Instance,
			ConverterParameter = isReadOnly,
			Bindings =
			{
				new Binding { RelativeSource = new RelativeSource(RelativeSourceMode.Self) },
				new Binding(nameof(RecipeRowViewModel.ForDepth)),
				new Binding(nameof(RecipeRowViewModel.IsPastStep)),
				new Binding(nameof(RecipeRowViewModel.IsCurrentStep)),
				columnKey is null ? Constant(false) : CellApplicabilityBinding.CreateInapplicableBinding(columnKey),
				columnKey is null ? Constant(false) : CellApplicabilityBinding.CreateChangedBinding(columnKey),
				new Binding(nameof(DataGridRow.IsSelected))
				{
					RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
					{
						AncestorType = typeof(DataGridRow),
					},
				},
			},
		};
	}

	private static Binding Constant(bool value)
	{
		return new Binding { Source = value };
	}

	private static ControlTheme? ResolveDefaultDataGridCellTheme()
	{
		return Application.Current?.FindResource(typeof(DataGridCell)) as ControlTheme;
	}
}
