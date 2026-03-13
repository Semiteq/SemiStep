using System.Collections.Immutable;
using System.Text;

using Csv.FsService;

using FluentResults;

using Serilog;

using Shared.Core;
using Shared.Results;
using Shared.ServiceContracts;

namespace Csv.Facade;

internal sealed class CsvService(CsvFileSerializer csvFileSerializer) : ICsvService
{
	private static readonly Encoding _fileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

	public async Task<Result<Recipe>> LoadAsync(string filePath, CancellationToken cancellationToken = default)
	{
		if (!File.Exists(filePath))
		{
			return Result.Fail<Recipe>($"Recipe file not found: {filePath}");
		}

		var fullText = await File.ReadAllTextAsync(filePath, _fileEncoding, cancellationToken);

		var (metadata, linesConsumed) = CsvMetadata.Deserialize(fullText);
		var bodyText = ExtractBody(fullText, linesConsumed);

		var result = csvFileSerializer.Deserialize(bodyText);

		if (result.IsFailed)
		{
			return result;
		}

		var warnings = new List<string>();

		if (metadata.Rows > 0 && metadata.Rows != result.Value.StepCount)
		{
			warnings.Add(
				$"Row count mismatch in '{filePath}': metadata says {metadata.Rows}, actual is {result.Value.StepCount}");
		}

		Log.Information("Loaded recipe from {FilePath}: {StepCount} steps", filePath, result.Value.StepCount);

		return Result.Ok(result.Value).WithWarnings(warnings);
	}

	public async Task SaveAsync(Recipe recipe, string filePath, CancellationToken cancellationToken = default)
	{
		var csvBody = csvFileSerializer.Serialize(recipe);
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

		Log.Information("Saved recipe to {FilePath}: {StepCount} steps", filePath, recipe.StepCount);
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

		return Math.Max(0, lineCount - 1);
	}
}
