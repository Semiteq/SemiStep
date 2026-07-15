using System.Globalization;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

using SemiStep.Core.Configuration;
using SemiStep.UI.RecipeGrid.Transposed;
using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid.Transposed;

// Pins the flattened cell-background converter to the exact document-order, last-match-wins
// precedence the removed TransposedGridStyles.axaml background rules produced. The oracle below is a
// literal transcription of those 29 rules (independent of the converter's nested-if shape), so
// agreement across the full state cross-product proves the flatten preserved every cell's brush.
[Trait("Component", "UI")]
[Trait("Category", "Unit")]
[Trait("Area", "RecipeGrid")]
public sealed class TransposedCellBackgroundConverterTests
{
	[AvaloniaFact]
	public void Convert_MatchesOldRulePrecedence_AcrossFullStateMatrix()
	{
		var host = BuildHostWithPalette(out var resources);
		var converter = TransposedCellBackgroundConverter.Instance;

		for (var depth = 0; depth <= 3; depth++)
		{
			foreach (var past in _bools)
			{
				foreach (var readOnly in _bools)
				{
					foreach (var applicable in _bools)
					{
						foreach (var changed in _bools)
						{
							foreach (var selected in _bools)
							{
								var expectedKey = ResolveExpectedKey(
									depth, past, readOnly, !applicable, changed, selected);
								var expectedBrush = resources[expectedKey];

								var actual = converter.Convert(
									[host, depth, past, applicable, changed, selected],
									typeof(IBrush),
									readOnly,
									CultureInfo.InvariantCulture);

								Assert.True(
									ReferenceEquals(expectedBrush, actual),
									$"depth={depth} past={past} readOnly={readOnly} applicable={applicable} "
									+ $"changed={changed} selected={selected}: expected '{expectedKey}'");
							}
						}
					}
				}
			}
		}
	}

	[AvaloniaFact]
	public void Convert_ClampsDepthAtThree()
	{
		var host = BuildHostWithPalette(out var resources);
		var converter = TransposedCellBackgroundConverter.Instance;

		var depthFive = converter.Convert(
			[host, 5, false, true, false, false],
			typeof(IBrush),
			false,
			CultureInfo.InvariantCulture);

		Assert.Same(resources[ExecutionPaletteInstaller.ExecRowDepth3BrushKey], depthFive);
	}

	[AvaloniaFact]
	public void Convert_TreatsNegativeDepthAsZero()
	{
		var host = BuildHostWithPalette(out var resources);
		var converter = TransposedCellBackgroundConverter.Instance;

		// depth<=0, not past, editable, applicable, not changed, not selected -> plain grid background.
		var negative = converter.Convert(
			[host, -1, false, true, false, false],
			typeof(IBrush),
			false,
			CultureInfo.InvariantCulture);

		Assert.Same(resources[CellPaletteInstaller.GridBackgroundBrushKey], negative);
	}

	private static readonly bool[] _bools = [false, true];

	// Literal, in-document-order transcription of the old Border.transposed-cell background rules.
	// The winner is the LAST matching rule (Avalonia last-match-wins among equal-priority setters).
	private static string ResolveExpectedKey(
		int depth, bool past, bool readOnly, bool inapplicable, bool changed, bool selected)
	{
		var winner = CellPaletteInstaller.GridBackgroundBrushKey;

		void Rule(bool condition, string key)
		{
			if (condition)
			{
				winner = key;
			}
		}

		Rule(readOnly, CellPaletteInstaller.CellReadOnlyDepth0BrushKey);
		Rule(inapplicable, CellPaletteInstaller.CellDisabledDepth0BrushKey);
		Rule(past, ExecutionPaletteInstaller.ExecRowDepth0PastBrushKey);
		Rule(depth == 1, ExecutionPaletteInstaller.ExecRowDepth1BrushKey);
		Rule(depth == 1 && past, ExecutionPaletteInstaller.ExecRowDepth1PastBrushKey);
		Rule(depth == 2, ExecutionPaletteInstaller.ExecRowDepth2BrushKey);
		Rule(depth == 2 && past, ExecutionPaletteInstaller.ExecRowDepth2PastBrushKey);
		Rule(depth == 3, ExecutionPaletteInstaller.ExecRowDepth3BrushKey);
		Rule(depth == 3 && past, ExecutionPaletteInstaller.ExecRowDepth3PastBrushKey);
		Rule(past && readOnly, CellPaletteInstaller.CellReadOnlyDepth0PastBrushKey);
		Rule(depth == 1 && readOnly, CellPaletteInstaller.CellReadOnlyDepth1BrushKey);
		Rule(depth == 1 && past && readOnly, CellPaletteInstaller.CellReadOnlyDepth1PastBrushKey);
		Rule(depth == 2 && readOnly, CellPaletteInstaller.CellReadOnlyDepth2BrushKey);
		Rule(depth == 2 && past && readOnly, CellPaletteInstaller.CellReadOnlyDepth2PastBrushKey);
		Rule(depth == 3 && readOnly, CellPaletteInstaller.CellReadOnlyDepth3BrushKey);
		Rule(depth == 3 && past && readOnly, CellPaletteInstaller.CellReadOnlyDepth3PastBrushKey);
		Rule(past && inapplicable, CellPaletteInstaller.CellDisabledDepth0PastBrushKey);
		Rule(depth == 1 && inapplicable, CellPaletteInstaller.CellDisabledDepth1BrushKey);
		Rule(depth == 1 && past && inapplicable, CellPaletteInstaller.CellDisabledDepth1PastBrushKey);
		Rule(depth == 2 && inapplicable, CellPaletteInstaller.CellDisabledDepth2BrushKey);
		Rule(depth == 2 && past && inapplicable, CellPaletteInstaller.CellDisabledDepth2PastBrushKey);
		Rule(depth == 3 && inapplicable, CellPaletteInstaller.CellDisabledDepth3BrushKey);
		Rule(depth == 3 && past && inapplicable, CellPaletteInstaller.CellDisabledDepth3PastBrushKey);
		Rule(changed, CellPaletteInstaller.CellChangedBrushKey);
		Rule(selected, CellPaletteInstaller.SelectionBackgroundBrushKey);
		Rule(selected && readOnly, CellPaletteInstaller.CellReadOnlySelectedBackgroundBrushKey);
		Rule(selected && inapplicable, CellPaletteInstaller.CellDisabledSelectedBackgroundBrushKey);
		Rule(selected && changed, CellPaletteInstaller.CellChangedSelectedBackgroundBrushKey);

		return winner;
	}

	private static Control BuildHostWithPalette(out IResourceDictionary resources)
	{
		var gridStyle = BuildDistinctPaletteGridStyle();
		var border = new Border();
		CellPaletteInstaller.Install(border.Resources, gridStyle);
		ExecutionPaletteInstaller.Install(border.Resources, gridStyle);
		resources = border.Resources;
		return border;
	}

	// Every key the converter can select gets a distinct color, so a wrong key resolves to a
	// different brush instance and the reference check fails loudly.
	private static GridStyleOptions BuildDistinctPaletteGridStyle()
	{
		return GridStyleOptions.Default with
		{
			GridBackgroundColor = "#101010",
			SelectionBackgroundColor = "#202020",
			CellChangedColor = "#303030",
			CellChangedSelectedColor = "#404040",
			ReadOnlyCellSelectedColor = "#505050",
			DisabledCellSelectedColor = "#606060",
			ExecutionDepth1Color = "#110000",
			ExecutionDepth2Color = "#120000",
			ExecutionDepth3Color = "#130000",
			ExecutionDepth0PastColor = "#140000",
			ExecutionDepth1PastColor = "#150000",
			ExecutionDepth2PastColor = "#160000",
			ExecutionDepth3PastColor = "#170000",
			ReadOnlyCellDepth0Color = "#210000",
			ReadOnlyCellDepth1Color = "#220000",
			ReadOnlyCellDepth2Color = "#230000",
			ReadOnlyCellDepth3Color = "#240000",
			ReadOnlyCellDepth0PastColor = "#250000",
			ReadOnlyCellDepth1PastColor = "#260000",
			ReadOnlyCellDepth2PastColor = "#270000",
			ReadOnlyCellDepth3PastColor = "#280000",
			DisabledCellDepth0Color = "#310000",
			DisabledCellDepth1Color = "#320000",
			DisabledCellDepth2Color = "#330000",
			DisabledCellDepth3Color = "#340000",
			DisabledCellDepth0PastColor = "#350000",
			DisabledCellDepth1PastColor = "#360000",
			DisabledCellDepth2PastColor = "#370000",
			DisabledCellDepth3PastColor = "#380000",
		};
	}
}
