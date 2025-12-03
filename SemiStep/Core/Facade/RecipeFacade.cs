using Core.Analyzer;
using Core.Entities;
using Core.Formulas;
using Core.Reasons.Errors;
using Core.Services;
using Core.Snapshot;
using Core.State;

using FluentResults;

using Microsoft.Extensions.Logging;

namespace Core.Facade;

/// <summary>
/// Coordinates mutation, formula application, and analysis.
/// </summary>
public sealed class RecipeFacade : IRecipeFacade
{
	private readonly RecipeMutator _mutator;
	private readonly FormulaApplicationCoordinator _formulaCoordinator;
	private readonly IActionRepository _actionRepository;
	private readonly IRecipeAnalyzer _analyzer;
	private readonly IRecipeStateManager _state;
	private readonly ILogger<RecipeFacade> _logger;

	private Recipe _recipe = Recipe.Empty;

	public RecipeAnalysisSnapshot CurrentSnapshot => _state.Current;
	public RecipeAnalysisSnapshot? LastValidSnapshot => _state.LastValid;

	public RecipeFacade(
		RecipeMutator mutator,
		FormulaApplicationCoordinator formulaCoordinator,
		IActionRepository actionRepository,
		IRecipeAnalyzer analyzer,
		IRecipeStateManager state,
		ILogger<RecipeFacade> logger)
	{
		_mutator = mutator;
		_formulaCoordinator = formulaCoordinator;
		_actionRepository = actionRepository;
		_analyzer = analyzer;
		_state = state;
		_logger = logger;

		var initialSnapshot = _analyzer.Analyze(_recipe);
		_state.Update(initialSnapshot);
	}

	public Result<RecipeAnalysisSnapshot> AddStep(int index)
	{
		var mutation = _mutator.AddDefaultStep(_recipe, index);
		if (mutation.IsFailed)
			return mutation.ToResult<RecipeAnalysisSnapshot>();

		return Commit(mutation.Value);
	}

	public Result<RecipeAnalysisSnapshot> RemoveStep(int index)
	{
		if (index < 0 || index >= _recipe.Steps.Count)
			return Result.Fail(new CoreIndexOutOfRangeError(index, _recipe.Steps.Count));

		var mutation = _mutator.RemoveStep(_recipe, index);
		if (mutation.IsFailed)
			return mutation.ToResult<RecipeAnalysisSnapshot>();

		return Commit(mutation.Value);
	}

	public Result<RecipeAnalysisSnapshot> ReplaceAction(int index, short actionId)
	{
		if (index < 0 || index >= _recipe.Steps.Count)
			return Result.Fail(new CoreIndexOutOfRangeError(index, _recipe.Steps.Count));

		var mutation = _mutator.ReplaceStepAction(_recipe, index, actionId);
		if (mutation.IsFailed)
			return mutation.ToResult<RecipeAnalysisSnapshot>();

		return Commit(mutation.Value);
	}

	public Result<RecipeAnalysisSnapshot> UpdateProperty(int index, ColumnIdentifier column, object value)
	{
		if (index < 0 || index >= _recipe.Steps.Count)
			return Result.Fail(new CoreIndexOutOfRangeError(index, _recipe.Steps.Count));

		var actionDefResult = GetActionDefinition(index);
		if (actionDefResult.IsFailed)
			return actionDefResult.ToResult<RecipeAnalysisSnapshot>();

		var mutation = _mutator.UpdateStepProperty(_recipe, index, column, value);
		if (mutation.IsFailed)
			return mutation.ToResult<RecipeAnalysisSnapshot>();

		var interim = mutation.Value;
		var step = interim.Steps[index];

		var formulaResult = _formulaCoordinator.ApplyIfExists(step, actionDefResult.Value, column);
		if (formulaResult.IsFailed)
		{
			_logger.LogWarning("Formula application failed at index {Index} column {Column}", index, column.Value);
			return formulaResult.ToResult<RecipeAnalysisSnapshot>();
		}

		var updatedSteps = interim.Steps.SetItem(index, formulaResult.Value);
		return Commit(new Recipe(updatedSteps));
	}

	public Result<RecipeAnalysisSnapshot> LoadRecipe(Recipe recipe)
	{
		return Commit(recipe);
	}

	private Result<RecipeAnalysisSnapshot> Commit(Recipe newRecipe)
	{
		_recipe = newRecipe;
		var snapshot = _analyzer.Analyze(_recipe);
		_state.Update(snapshot);
		return Result.Ok(snapshot);
	}

	private Result<ActionDefinition> GetActionDefinition(int index)
	{
		var step = _recipe.Steps[index];
		if (!step.Properties.TryGetValue(MandatoryColumns.Action, out var property) || property == null)
			return Result.Fail(new CoreStepActionPropertyNullError(index));

		var valResult = property.GetValue<short>();

		if (valResult.IsFailed)
			return valResult.ToResult<ActionDefinition>();

		return _actionRepository.GetActionDefinitionById(valResult.Value);
	}

	public Result<RecipeAnalysisSnapshot> InsertSteps(int index, IReadOnlyList<Step> steps)
	{
		if (steps == null)
			throw new ArgumentNullException(nameof(steps));

		if (steps.Count == 0)
			return Result.Ok(CurrentSnapshot);

		var mutation = _mutator.InsertSteps(_recipe, index, steps);

		if (mutation.IsFailed)
			return mutation.ToResult<RecipeAnalysisSnapshot>();

		return Commit(mutation.Value);
	}

	public Result<RecipeAnalysisSnapshot> DeleteSteps(IReadOnlyCollection<int> indices)
	{
		if (indices == null)
			throw new ArgumentNullException(nameof(indices));

		if (indices.Count == 0)
			return Result.Ok(CurrentSnapshot);

		var mutation = _mutator.RemoveSteps(_recipe, indices);

		if (mutation.IsFailed)
			return mutation.ToResult<RecipeAnalysisSnapshot>();

		return Commit(mutation.Value);
	}
}
