using FluentResults;

using Shared.Core;

namespace Shared.ServiceContracts;

public interface ICsvService
{
	Task<Result<Recipe>> LoadAsync(string filePath, CancellationToken cancellationToken = default);

	Task SaveAsync(Recipe recipe, string filePath, CancellationToken cancellationToken = default);
}
