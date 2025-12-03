using Core.Entities;
using Core.Snapshot;

namespace Core.Analyzer;

/// <summary>
/// Orchestrates full recipe analysis pipeline.
/// </summary>
public interface IRecipeAnalyzer
{
	RecipeAnalysisSnapshot Analyze(Recipe recipe);
}
