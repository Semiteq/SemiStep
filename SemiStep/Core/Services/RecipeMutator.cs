using Core.Domain;
using Core.Entities;

using FluentResults;

namespace Core.Services;

internal sealed class RecipeMutator
{
	internal Result<Recipe> AddDefaultStep(Recipe recipe, int index) => Result.Fail("Not implemented.");
	internal Result<Recipe> RemoveStep(Recipe recipe, int index) => Result.Fail("Not implemented.");

	internal Result<Recipe> ReplaceStepAction(Recipe recipe, int index, short actionId) =>
		Result.Fail("Not implemented.");

	internal Result<Recipe> UpdateStepProperty(Recipe recipe, int index, ColumnId columnId, PrimitiveValueDto? value) =>
		Result.Fail("Not implemented.");

	internal Result<Recipe> InsertSteps(Recipe recipe, int index, IReadOnlyList<StepDto> steps) =>
		Result.Fail("Not implemented.");

	internal Result<Recipe> RemoveSteps(Recipe recipe, IReadOnlyCollection<int> indices) =>
		Result.Fail("Not implemented.");
}
