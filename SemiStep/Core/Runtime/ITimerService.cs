using Core.Snapshot;

namespace Core.Runtime;

/// <summary>
/// Computes remaining time information based on snapshot + runtime PLC data.
/// </summary>
public interface ITimerService
{
	public event Action<TimeSpan, TimeSpan>? TimesUpdated;
	void Reset();
	void UpdateRuntime(RecipeRuntimeSnapshot runtimeSnapshot, RecipeAnalysisSnapshot analysisSnapshot);
}
