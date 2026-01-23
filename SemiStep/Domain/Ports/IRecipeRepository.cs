namespace Domain.Ports;

public interface IRecipeRepository
{
	Task<Recipe.Entities.Recipe> LoadAsync(string filePath, CancellationToken cancellationToken = default);

	Task SaveAsync(Recipe.Entities.Recipe recipe, string filePath, CancellationToken cancellationToken = default);

	bool CanHandle(string filePath);
}
