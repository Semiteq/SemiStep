using FluentResults;

using TypesShared.Core;

namespace TypesShared.Domain;

public interface ICsvService
{
	Task<Result<Recipe>> LoadAsync(string filePath);

	Task SaveAsync(Recipe recipe, string filePath);
}
