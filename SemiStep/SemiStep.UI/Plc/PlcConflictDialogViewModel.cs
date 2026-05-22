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
}
