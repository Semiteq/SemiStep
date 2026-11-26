using Avalonia.Data.Converters;

using SemiStep.UI.Localization;

namespace SemiStep.UI.RecipeGrid;

/// <summary>Actions whose step time is a ramp time that the step duration does not count.</summary>
internal static class RampActions
{
	// MBE "t°C плавно" and "P% плавно": immediate actions, so TimingCalculator counts zero for their time.
	private static readonly HashSet<int> _rampActionKeys = [110, 140];

	/// <summary>Action key to the ramp time tooltip of its step duration cell, or none.</summary>
	public static readonly FuncValueConverter<int, string?> ToolTipConverter =
		new(actionKey => IsRamp(actionKey) ? Resources.RampTimeToolTip : null);

	public static bool IsRamp(int actionKey)
	{
		return _rampActionKeys.Contains(actionKey);
	}
}
