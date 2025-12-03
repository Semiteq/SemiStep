using Core.Entities;
using Core.Snapshot;

using FluentResults;

namespace Core.Facade;

/// <summary>
/// High-level mutation API producing analysis snapshots.
/// </summary>
public interface IRecipeFacade
{
	RecipeAnalysisSnapshot CurrentSnapshot { get; }
	RecipeAnalysisSnapshot? LastValidSnapshot { get; }

	Result<RecipeAnalysisSnapshot> AddStep(int index);
	Result<RecipeAnalysisSnapshot> RemoveStep(int index);
	Result<RecipeAnalysisSnapshot> ReplaceAction(int index, short actionId);
	Result<RecipeAnalysisSnapshot> UpdateProperty(int index, ColumnIdentifier column, object value);
	Result<RecipeAnalysisSnapshot> LoadRecipe(Recipe recipe);
	Result<RecipeAnalysisSnapshot> InsertSteps(int index, IReadOnlyList<Step> steps);
	Result<RecipeAnalysisSnapshot> DeleteSteps(IReadOnlyCollection<int> indices);
}
