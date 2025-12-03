namespace Core.Analyzer;

public interface ILoopSemanticEvaluator
{
	LoopSemanticsResult Evaluate(LoopParseResult parseResult);
}
