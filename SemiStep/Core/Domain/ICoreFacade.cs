using FluentResults;

namespace Core.Domain;

public interface ICoreFacade
{
	CoreSnapshot CurrentSnapshot { get; }
	CoreSnapshot? LastValidSnapshot { get; }

	Result<CoreSnapshot> AddStep(int index);
	Result<CoreSnapshot> RemoveStep(int index);
	Result<CoreSnapshot> ReplaceAction(int index, short actionId);

	Result<CoreSnapshot> UpdateProperty(int index, string columnId, PrimitiveValueDto? value);

	Result<CoreSnapshot> LoadRecipe(RecipeDto recipe);
	Result<CoreSnapshot> InsertSteps(int index, IReadOnlyList<StepDto> steps);
	Result<CoreSnapshot> DeleteSteps(IReadOnlyCollection<int> indices);
}
