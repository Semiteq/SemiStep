using SemiStep.Core.Plc.State;

namespace SemiStep.Core.Plc;

public interface IS7ExecutionStream
{
	bool IsRecipeActive { get; }
	IObservable<PlcExecutionInfo> ExecutionState { get; }
}
