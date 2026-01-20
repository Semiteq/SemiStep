using Core.Analysis;
using Core.Entities;

namespace SemiStep.Application.State;

public sealed class RecipeStateManager
{
	private Recipe _current = Recipe.Empty;
	private Recipe _lastValid = Recipe.Empty;
	private AnalysisResult? _lastAnalysis;

	public Recipe Current => _current;
	public Recipe LastValid => _lastValid;
	public AnalysisResult? LastAnalysis => _lastAnalysis;
	public bool IsDirty { get; private set; }
	public bool IsValid => _lastAnalysis?.IsValid ?? false;

	public void Update(Recipe newRecipe, AnalysisResult analysis)
	{
		_current = newRecipe;
		_lastAnalysis = analysis;
		IsDirty = true;

		if (analysis.IsValid)
			_lastValid = newRecipe;
	}

	public void Load(Recipe recipe, AnalysisResult analysis)
	{
		_current = recipe;
		_lastAnalysis = analysis;
		IsDirty = false;

		if (analysis.IsValid)
			_lastValid = recipe;
	}

	public void MarkSaved()
	{
		IsDirty = false;
	}

	public void Reset()
	{
		_current = Recipe.Empty;
		_lastValid = Recipe.Empty;
		_lastAnalysis = null;
		IsDirty = false;
	}
}
