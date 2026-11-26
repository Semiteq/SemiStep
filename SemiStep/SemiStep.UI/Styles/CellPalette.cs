using Avalonia.Media;
using Avalonia.Media.Immutable;

using SemiStep.Core.Configuration;

namespace SemiStep.UI.Styles;

/// <summary>Base colour of a cell before any tint: orange marks win over grey, grey over the grid background.</summary>
internal enum CellBase
{
	Plain,
	Inapplicable,
	ReadOnly,
	Changed,
}

internal enum CellExecutionState
{
	Idle,
	Past,
	Running,
}

/// <summary>
/// Every cell colour, blended once per config load in the order base, loop tint, execution green, selection.
/// See docs/architecture/grid-style-configuration.md.
/// </summary>
internal sealed class CellPalette
{
	public const string ResourceKey = "CellPalette";
	public const int MaxDepth = 3;

	private const int WeightScale = 1000;
	private const int BaseCount = 4;
	private const int DepthCount = MaxDepth + 1;
	private const int ExecutionStateCount = 3;
	private const int SelectionStateCount = 2;

	private readonly IImmutableSolidColorBrush[] _brushes;

	private CellPalette(IImmutableSolidColorBrush[] brushes)
	{
		_brushes = brushes;
	}

	public static CellPalette Build(GridStyleOptions gridStyle)
	{
		var brushes = new IImmutableSolidColorBrush[BaseCount * DepthCount * ExecutionStateCount * SelectionStateCount];
		foreach (var cellBase in Enum.GetValues<CellBase>())
		{
			for (var depth = 0; depth <= MaxDepth; depth++)
			{
				foreach (var state in Enum.GetValues<CellExecutionState>())
				{
					foreach (var selected in (bool[])[false, true])
					{
						var color = Compose(gridStyle, cellBase, depth, state, selected);
						brushes[IndexOf(cellBase, depth, state, selected)] =
							new ImmutableSolidColorBrush(color.ToMediaColor());
					}
				}
			}
		}

		return new CellPalette(brushes);
	}

	public static CellBase BaseOf(bool isChanged, bool isInapplicable, bool isReadOnly)
	{
		return isChanged ? CellBase.Changed
			: isInapplicable ? CellBase.Inapplicable
			: isReadOnly ? CellBase.ReadOnly
			: CellBase.Plain;
	}

	public static CellExecutionState ExecutionStateOf(bool isRunning, bool isPast)
	{
		return isRunning ? CellExecutionState.Running
			: isPast ? CellExecutionState.Past
			: CellExecutionState.Idle;
	}

	/// <summary>Integer blend per channel, weight in thousandths, rounded half up; the result is opaque.</summary>
	public static StyleColor Blend(StyleColor under, StyleColor over, int weightThousandths)
	{
		var keep = WeightScale - weightThousandths;
		return new StyleColor(
			byte.MaxValue,
			(byte)(((under.R * keep) + (over.R * weightThousandths) + (WeightScale / 2)) / WeightScale),
			(byte)(((under.G * keep) + (over.G * weightThousandths) + (WeightScale / 2)) / WeightScale),
			(byte)(((under.B * keep) + (over.B * weightThousandths) + (WeightScale / 2)) / WeightScale));
	}

	public IImmutableSolidColorBrush Resolve(CellBase cellBase, int depth, CellExecutionState state, bool selected)
	{
		return _brushes[IndexOf(cellBase, Math.Clamp(depth, 0, MaxDepth), state, selected)];
	}

	private static StyleColor Compose(
		GridStyleOptions gridStyle,
		CellBase cellBase,
		int depth,
		CellExecutionState state,
		bool selected)
	{
		var color = cellBase switch
		{
			CellBase.Changed => gridStyle.ChangedCells.Changed,
			CellBase.Inapplicable => gridStyle.DisabledCell,
			CellBase.ReadOnly => gridStyle.ReadOnlyCell,
			_ => gridStyle.Chrome.GridBackground,
		};

		var loopWeight = depth switch
		{
			1 => gridStyle.Loop.Depth1Weight,
			2 => gridStyle.Loop.Depth2Weight,
			3 => gridStyle.Loop.Depth3Weight,
			_ => 0,
		};
		color = Blend(color, gridStyle.Loop.Color, ToThousandths(loopWeight));

		var executionWeight = state switch
		{
			CellExecutionState.Running => gridStyle.Execution.RunningWeight,
			CellExecutionState.Past => gridStyle.Execution.PastWeight,
			_ => 0,
		};
		color = Blend(color, gridStyle.Execution.Running, ToThousandths(executionWeight));

		return selected
			? Blend(color, gridStyle.Selection.Accent, ToThousandths(gridStyle.Selection.Weight))
			: color;
	}

	private static int ToThousandths(double weight)
	{
		return (int)Math.Round(weight * WeightScale, MidpointRounding.AwayFromZero);
	}

	private static int IndexOf(CellBase cellBase, int depth, CellExecutionState state, bool selected)
	{
		var stateIndex = ((((int)cellBase * DepthCount) + depth) * ExecutionStateCount) + (int)state;
		return (stateIndex * SelectionStateCount) + (selected ? 1 : 0);
	}
}
