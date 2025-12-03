using Core.Entities;

namespace Core.Analyzer;

public interface ITimingCalculator
{
	TimingResult Calculate(Recipe recipe, LoopSemanticsResult loopSemantics);
}
