using System.Collections.Immutable;
using System.Text;

using Core.Entities;

using Domain.Ports;

using Serilog;

namespace Csv;

public sealed class CsvRecipeRepository(CsvSerializer csvSerializer, ILogger logger) : IRecipeRepository
{
	private static readonly Encoding _fileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

	public async Task<Recipe> LoadAsync(string filePath, CancellationToken cancellationToken = default)
	{
		if (!File.Exists(filePath))
		{
			throw new FileNotFoundException($"Recipe file not found: {filePath}", filePath);
		}

		var fullText = await File.ReadAllTextAsync(filePath, _fileEncoding, cancellationToken);

		var (metadata, linesConsumed) = CsvMetadata.Deserialize(fullText);
		var bodyText = ExtractBody(fullText, linesConsumed);

		var recipe = csvSerializer.Deserialize(bodyText);

		VerifyRowCount(metadata, recipe.StepCount, filePath);

		logger.Information("Loaded recipe from {FilePath}: {StepCount} steps", filePath, recipe.StepCount);

		return recipe;
	}

	public async Task SaveAsync(Recipe recipe, string filePath, CancellationToken cancellationToken = default)
	{
		var csvBody = csvSerializer.Serialize(recipe);
		var dataRowCount = CountDataRows(csvBody);

		var metadata = new CsvMetadata(
			Rows: dataRowCount,
			Extras: ImmutableDictionary<string, string>.Empty
				.Add("ExportedAtUtc", DateTime.UtcNow.ToString("O")));

		var tempPath = filePath + ".tmp";

		try
		{
			await using (var stream = new FileStream(
				tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
			await using (var writer = new StreamWriter(stream, _fileEncoding))
			{
				CsvMetadata.Serialize(writer, metadata);
				await writer.WriteAsync(csvBody);
			}

			File.Move(tempPath, filePath, overwrite: true);
		}
		finally
		{
			if (File.Exists(tempPath))
			{
				try
				{
					File.Delete(tempPath);
				}
				catch
				{
					// Best effort cleanup
				}
			}
		}

		logger.Information("Saved recipe to {FilePath}: {StepCount} steps", filePath, recipe.StepCount);
	}

	public bool CanHandle(string filePath)
	{
		return filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
	}

	private static string ExtractBody(string fullText, int metadataLines)
	{
		if (metadataLines == 0)
		{
			return fullText;
		}

		using var reader = new StringReader(fullText);
		for (var i = 0; i < metadataLines; i++)
		{
			reader.ReadLine();
		}

		return reader.ReadToEnd();
	}

	private void VerifyRowCount(CsvMetadata metadata, int actualCount, string filePath)
	{
		if (metadata.Rows > 0 && metadata.Rows != actualCount)
		{
			logger.Warning(
				"Row count mismatch in {FilePath}: metadata says {Expected}, actual is {Actual}",
				filePath, metadata.Rows, actualCount);
		}
	}

	private static int CountDataRows(string csvBody)
	{
		if (string.IsNullOrEmpty(csvBody))
		{
			return 0;
		}

		using var reader = new StringReader(csvBody);
		var lineCount = 0;

		while (reader.ReadLine() is not null)
		{
			lineCount++;
		}

		// Subtract 1 for the header row
		return Math.Max(0, lineCount - 1);
	}
}
