using Recipe.Analysis;

namespace Domain.State;

public sealed class RecipeStateManager
{
	private Recipe.Entities.Recipe _lastValid = Recipe.Entities.Recipe.Empty;
	public Recipe.Entities.Recipe Current { get; private set; } = Recipe.Entities.Recipe.Empty;
	public AnalysisResult? LastAnalysis { get; private set; }
	public bool IsDirty { get; private set; }
	public bool IsValid => LastAnalysis?.IsValid ?? false;

	public void Update(Recipe.Entities.Recipe newRecipe, AnalysisResult analysis)
	{
		Current = newRecipe;
		LastAnalysis = analysis;
		IsDirty = true;

		if (analysis.IsValid)
		{
			_lastValid = newRecipe;
		}
	}

	public void Load(Recipe.Entities.Recipe recipe, AnalysisResult analysis)
	{
		Current = recipe;
		LastAnalysis = analysis;
		IsDirty = false;

		if (analysis.IsValid)
		{
			_lastValid = recipe;
		}
	}

	public void MarkSaved()
	{
		IsDirty = false;
	}

	public void Reset()
	{
		Current = Recipe.Entities.Recipe.Empty;
		_lastValid = Recipe.Entities.Recipe.Empty;
		LastAnalysis = null;
		IsDirty = false;
	}
}
