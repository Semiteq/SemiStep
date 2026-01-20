using Core.Analysis;
using Core.Entities;
using Core.Exceptions;
using Core.Metadata;
using Core.Services;

using SemiStep.Application.State;

namespace SemiStep.Application.Services;

public sealed class RecipeApplicationService
{
	private readonly RecipeStateManager _state;
	private readonly RecipeMutator _mutator;
	private readonly PropertyValidator _validator;
	private readonly RecipeAnalyzer _analyzer;
	private readonly IMetadataProvider _metadata;

	public RecipeApplicationService(
		RecipeStateManager state,
		RecipeMutator mutator,
		PropertyValidator validator,
		RecipeAnalyzer analyzer,
		IMetadataProvider metadata)
	{
		_state = state;
		_mutator = mutator;
		_validator = validator;
		_analyzer = analyzer;
		_metadata = metadata;
	}

	public Recipe CurrentRecipe => _state.Current;
	public bool IsDirty => _state.IsDirty;
	public bool IsValid => _state.IsValid;
	public AnalysisResult? LastAnalysis => _state.LastAnalysis;

	public void AddStep(string actionKey)
	{
		var newRecipe = _mutator.AddStep(_state.Current, actionKey);
		var analysis = _analyzer.Analyze(newRecipe);
		_state.Update(newRecipe, analysis);
	}

	public void InsertStep(int index, string actionKey)
	{
		var newRecipe = _mutator.InsertStep(_state.Current, index, actionKey);
		var analysis = _analyzer.Analyze(newRecipe);
		_state.Update(newRecipe, analysis);
	}

	public void RemoveStep(int index)
	{
		var newRecipe = _mutator.RemoveStep(_state.Current, index);
		var analysis = _analyzer.Analyze(newRecipe);
		_state.Update(newRecipe, analysis);
	}

	public void UpdateProperty(int stepIndex, ColumnId column, object value)
	{
		var validation = _validator.Validate(column, value);
		if (!validation.IsValid)
			throw new PropertyValidationException(validation);

		var propertyValue = CreatePropertyValue(column, value);
		var newRecipe = _mutator.UpdateProperty(_state.Current, stepIndex, column, propertyValue);
		var analysis = _analyzer.Analyze(newRecipe);
		_state.Update(newRecipe, analysis);
	}

	public void LoadRecipe(Recipe recipe)
	{
		var analysis = _analyzer.Analyze(recipe);
		_state.Load(recipe, analysis);
	}

	public void NewRecipe()
	{
		_state.Reset();
	}

	public void MarkSaved()
	{
		_state.MarkSaved();
	}

	private PropertyValue CreatePropertyValue(ColumnId column, object value)
	{
		var type = value switch
		{
			int => PropertyType.Int,
			float => PropertyType.Float,
			string => PropertyType.String,
			bool => PropertyType.Bool,
			_ => throw new ArgumentException($"Unsupported value type: {value.GetType()}")
		};
		return new PropertyValue(value, type);
	}
}
