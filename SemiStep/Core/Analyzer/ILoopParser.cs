using Core.Entities;

namespace Core.Analyzer;

public interface ILoopParser
{
	LoopParseResult Parse(Recipe recipe);
}
