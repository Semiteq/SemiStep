using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;

using SemiStep.UI.Styles;

namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// Both grids' cell background: the inputs are the resource host, loop depth, past, running, inapplicable,
/// changed and selected, in that order; the parameter says whether the column is read-only.
/// </summary>
internal sealed class CellBackgroundConverter : IMultiValueConverter
{
	public static readonly CellBackgroundConverter Instance = new();

	// The palette a host found first; the lookup walks the tree up to the window, and a cell re-evaluates on every
	// state change of its row.
	private static readonly AttachedProperty<CellPalette?> _cachedPaletteProperty =
		AvaloniaProperty.RegisterAttached<CellBackgroundConverter, Control, CellPalette?>("CachedPalette");

	private const int InputCount = 7;

	public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		if (values.Count < InputCount
			|| values[0] is not Control host
			|| PaletteOf(host) is not { } palette)
		{
			return AvaloniaProperty.UnsetValue;
		}

		var depth = values[1] as int? ?? 0;
		var isPast = values[2] as bool? ?? false;
		var isRunning = values[3] as bool? ?? false;
		var isInapplicable = values[4] as bool? ?? false;
		var isChanged = values[5] as bool? ?? false;
		var isSelected = values[6] as bool? ?? false;
		var isReadOnly = parameter as bool? ?? false;

		return palette.Resolve(
			CellPalette.BaseOf(isChanged, isInapplicable, isReadOnly),
			depth,
			CellPalette.ExecutionStateOf(isRunning, isPast),
			isSelected);
	}

	// A miss is not cached: a pooled transposed cell evaluates once while detached, before it can reach the palette.
	private static CellPalette? PaletteOf(Control host)
	{
		if (host.GetValue(_cachedPaletteProperty) is { } cached)
		{
			return cached;
		}

		if (!host.TryFindResource(CellPalette.ResourceKey, out var resource) || resource is not CellPalette palette)
		{
			return null;
		}

		host.SetValue(_cachedPaletteProperty, palette);
		return palette;
	}
}
