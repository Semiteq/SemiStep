using System.Globalization;

using SemiStep.UI.Localization;

namespace SemiStep.UI.Plc;

internal sealed class PlcConflictDialogViewModel(int localStepCount, int plcStepCount)
{
	public int LocalStepCount { get; } = localStepCount;

	public int PlcStepCount { get; } = plcStepCount;

	public string LocalStepCountText => FormatLocalStepCount(LocalStepCount);

	public string PlcStepCountText => FormatPlcStepCount(PlcStepCount);

	internal static string FormatLocalStepCount(int stepCount)
	{
		return string.Format(CultureInfo.InvariantCulture, Resources.PlcConflictLocalSteps, stepCount);
	}

	internal static string FormatPlcStepCount(int stepCount)
	{
		return string.Format(CultureInfo.InvariantCulture, Resources.PlcConflictPlcSteps, stepCount);
	}
}
