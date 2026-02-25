using Core.Entities;

using Domain.Ports;

namespace Tests.Helpers;

public sealed class StubRecipeRepository : IRecipeRepository
{
	public Task<Recipe> LoadAsync(string filePath, CancellationToken cancellationToken = default)
	{
		throw new NotSupportedException("StubRecipeRepository does not support loading.");
	}

	public Task SaveAsync(Recipe recipe, string filePath, CancellationToken cancellationToken = default)
	{
		throw new NotSupportedException("StubRecipeRepository does not support saving.");
	}

	public bool CanHandle(string filePath)
	{
		return false;
	}
}
