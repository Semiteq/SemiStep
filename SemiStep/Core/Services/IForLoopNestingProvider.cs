namespace Core.Services;

public interface IForLoopNestingProvider
{
	int GetNestingDepth(int stepIndex);
}
