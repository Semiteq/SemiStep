using Core.Entities;

using Domain.Models;
using Domain.Ports;

namespace Tests.Helpers;

public sealed class StubCsvService : ICsvService
{
	public Task<CsvLoadResult> LoadAsync(string filePath, CancellationToken cancellationToken = default)
	{
		throw new NotSupportedException("StubCsvService does not support loading.");
	}

	public Task SaveAsync(Recipe recipe, string filePath, CancellationToken cancellationToken = default)
	{
		throw new NotSupportedException("StubCsvService does not support saving.");
	}
}
