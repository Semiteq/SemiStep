using Core.Entities;
using Core.Reasons;
using Core.Services;

using FluentResults;

using Microsoft.Extensions.Logging;

namespace Core.Domain;

public sealed class CoreFacade : ICoreFacade
{
	private readonly RecipeImporter _recipeImporter;
	private readonly RecipeMutator _recipeMutator;
	private Recipe _recipe = Recipe.Empty;

	public CoreSnapshot CurrentSnapshot => new();
	public CoreSnapshot? LastValidSnapshot => null;

	internal CoreFacade(
		RecipeImporter recipeImporter,
		ILogger<CoreFacade> logger, RecipeMutator recipeMutator)
	{
		ArgumentNullException.ThrowIfNull(recipeMutator);
		ArgumentNullException.ThrowIfNull(recipeImporter);
		ArgumentNullException.ThrowIfNull(logger);

		_recipeImporter = recipeImporter;
		_recipeMutator = recipeMutator;
	}

	public Result<CoreSnapshot> AddStep(int index)
	{
		return _recipeMutator.AddDefaultStep(_recipe, index).Bind(Commit);
	}

	public Result<CoreSnapshot> RemoveStep(int index)
	{
		if (index < 0 || index >= _recipe.Steps.Count)
		{
			return Result.Fail(Errors.IndexOutOfRange
				.WithMetadata("index", index)
				.WithMetadata("count", _recipe.Steps.Count));
		}

		return _recipeMutator.RemoveStep(_recipe, index).Bind(Commit);
	}

	public Result<CoreSnapshot> ReplaceAction(int index, short actionId)
	{
		if (index < 0 || index >= _recipe.Steps.Count)
		{
			return Result.Fail(Errors.IndexOutOfRange
				.WithMetadata("index", index)
				.WithMetadata("count", _recipe.Steps.Count));
		}

		return _recipeMutator.ReplaceStepAction(_recipe, index, actionId).Bind(Commit);
	}

	public Result<CoreSnapshot> UpdateProperty(int index, string columnId, PrimitiveValueDto? value)
	{
		if (index < 0 || index >= _recipe.Steps.Count)
		{
			return Result.Fail(Errors.IndexOutOfRange
				.WithMetadata("index", index)
				.WithMetadata("count", _recipe.Steps.Count));
		}

		if (string.IsNullOrWhiteSpace(columnId))
		{
			return Result.Fail(Errors.StepColumnNotFound.WithMetadata("columnId", columnId));
		}

		var column = new ColumnId(columnId);

		var mutation = _recipeMutator.UpdateStepProperty(_recipe, index, column, value);
		if (mutation.IsFailed)
		{
			return mutation.ToResult<CoreSnapshot>();
		}

		return Commit(mutation.Value);
	}

	public Result<CoreSnapshot> LoadRecipe(RecipeDto recipe)
	{
		return _recipeImporter.Import(recipe).Bind(Commit);
	}

	public Result<CoreSnapshot> InsertSteps(int index, IReadOnlyList<StepDto> steps)
	{
		ArgumentNullException.ThrowIfNull(steps);

		if (steps.Count == 0)
		{
			return Result.Ok(CurrentSnapshot);
		}

		return _recipeMutator.InsertSteps(_recipe, index, steps).Bind(Commit);
	}

	public Result<CoreSnapshot> DeleteSteps(IReadOnlyCollection<int> indices)
	{
		ArgumentNullException.ThrowIfNull(indices);

		if (indices.Count == 0)
		{
			return Result.Ok(CurrentSnapshot);
		}

		return _recipeMutator.RemoveSteps(_recipe, indices).Bind(Commit);
	}

	private Result<CoreSnapshot> Commit(Recipe newRecipe)
	{
		_recipe = newRecipe;
		return Result.Ok();
	}
}
