using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

using SemiStep.UI.Styles;

namespace SemiStep.UI.RecipeGrid.Transposed;

// Collapses the transposed cell's background state matrix into one binding, replacing the 29
// background setter rules that previously lived in TransposedGridStyles.axaml. The
// winning brush reproduces those rules' document-order, last-match-wins precedence exactly:
//   selected            -> changed > inapplicable > read-only > plain selection tint
//   else changed        -> the changed highlight (beats depth/read-only/inapplicable, loses to selection)
//   else inapplicable   -> disabled palette, per loop depth + past-step
//   else read-only      -> read-only palette, per loop depth + past-step
//   else                -> execution depth/past tint (depth-0 idle is the plain grid background)
// Brushes resolve through the target Border as a resource host, so the same visual-tree + application
// lookup {DynamicResource} used still applies (works for both app- and window-scoped palette installs).
internal sealed class TransposedCellBackgroundConverter : IMultiValueConverter
{
	public static readonly TransposedCellBackgroundConverter Instance = new();

	public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		if (values.Count < 7 || values[0] is not Control host)
		{
			return AvaloniaProperty.UnsetValue;
		}

		var depth = values[1] as int? ?? 0;
		var isPastStep = values[2] as bool? ?? false;
		var isReadOnly = values[3] as bool? ?? false;
		var isApplicable = values[4] as bool? ?? true;
		var isChanged = values[5] as bool? ?? false;
		var isSelected = values[6] as bool? ?? false;

		var key = ResolveBrushKey(depth, isPastStep, isReadOnly, !isApplicable, isChanged, isSelected);

		if (host.TryFindResource(key, out var value) && value is IBrush brush)
		{
			return brush;
		}

		return AvaloniaProperty.UnsetValue;
	}

	private static string ResolveBrushKey(
		int depth,
		bool past,
		bool readOnly,
		bool inapplicable,
		bool changed,
		bool selected)
	{
		depth = Math.Clamp(depth, 0, 3);

		if (selected)
		{
			if (changed)
			{
				return CellPaletteInstaller.CellChangedSelectedBackgroundBrushKey;
			}

			if (inapplicable)
			{
				return CellPaletteInstaller.CellDisabledSelectedBackgroundBrushKey;
			}

			if (readOnly)
			{
				return CellPaletteInstaller.CellReadOnlySelectedBackgroundBrushKey;
			}

			return CellPaletteInstaller.SelectionBackgroundBrushKey;
		}

		if (changed)
		{
			return CellPaletteInstaller.CellChangedBrushKey;
		}

		if (inapplicable)
		{
			return depth switch
			{
				0 => past ? CellPaletteInstaller.CellDisabledDepth0PastBrushKey
					: CellPaletteInstaller.CellDisabledDepth0BrushKey,
				1 => past ? CellPaletteInstaller.CellDisabledDepth1PastBrushKey
					: CellPaletteInstaller.CellDisabledDepth1BrushKey,
				2 => past ? CellPaletteInstaller.CellDisabledDepth2PastBrushKey
					: CellPaletteInstaller.CellDisabledDepth2BrushKey,
				_ => past ? CellPaletteInstaller.CellDisabledDepth3PastBrushKey
					: CellPaletteInstaller.CellDisabledDepth3BrushKey,
			};
		}

		if (readOnly)
		{
			return depth switch
			{
				0 => past ? CellPaletteInstaller.CellReadOnlyDepth0PastBrushKey
					: CellPaletteInstaller.CellReadOnlyDepth0BrushKey,
				1 => past ? CellPaletteInstaller.CellReadOnlyDepth1PastBrushKey
					: CellPaletteInstaller.CellReadOnlyDepth1BrushKey,
				2 => past ? CellPaletteInstaller.CellReadOnlyDepth2PastBrushKey
					: CellPaletteInstaller.CellReadOnlyDepth2BrushKey,
				_ => past ? CellPaletteInstaller.CellReadOnlyDepth3PastBrushKey
					: CellPaletteInstaller.CellReadOnlyDepth3BrushKey,
			};
		}

		return depth switch
		{
			0 => past ? ExecutionPaletteInstaller.ExecRowDepth0PastBrushKey
				: CellPaletteInstaller.GridBackgroundBrushKey,
			1 => past ? ExecutionPaletteInstaller.ExecRowDepth1PastBrushKey
				: ExecutionPaletteInstaller.ExecRowDepth1BrushKey,
			2 => past ? ExecutionPaletteInstaller.ExecRowDepth2PastBrushKey
				: ExecutionPaletteInstaller.ExecRowDepth2BrushKey,
			_ => past ? ExecutionPaletteInstaller.ExecRowDepth3PastBrushKey
				: ExecutionPaletteInstaller.ExecRowDepth3BrushKey,
		};
	}
}
