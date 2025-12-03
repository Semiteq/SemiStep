using Core.Snapshot;

namespace Core.State;

public interface IRecipeStateManager
{
	RecipeAnalysisSnapshot Current { get; }
	RecipeAnalysisSnapshot? LastValid { get; }
	void Update(RecipeAnalysisSnapshot snapshot);
}
