using System.Globalization;

using SemiStep.UI.Localization;

namespace SemiStep.UI.Plc;

internal sealed class PlcConflictDialogViewModel
{
	public PlcConflictDialogViewModel(int localStepCount, int plcStepCount)
	{
		LocalStepCount = localStepCount;
		PlcStepCount = plcStepCount;
	}

	public int LocalStepCount { get; }

	public int PlcStepCount { get; }

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
