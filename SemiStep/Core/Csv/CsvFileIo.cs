using System.Collections.Immutable;
using System.Text;

namespace Csv;

internal static class CsvFileIo
{
	private static readonly Encoding _fileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

	internal static async Task<(string Body, CsvMetadata Metadata)> ReadRecipeFileAsync(string filePath)
	{
		var fullText = await File.ReadAllTextAsync(filePath, _fileEncoding);

		var (metadata, linesConsumed) = CsvMetadata.Deserialize(fullText);
		var body = ExtractBody(fullText, linesConsumed);

		return (body, metadata);
	}

	internal static async Task WriteRecipeFileAsync(
		string csvBody,
		CsvMetadata metadata,
		string filePath)
	{
		var tempPath = filePath + ".tmp";

		try
		{
			await using (var stream = new FileStream(
							 tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
			await using (var writer = new StreamWriter(stream, _fileEncoding))
			{
				CsvMetadata.Serialize(writer, metadata);
				await writer.WriteAsync(csvBody.AsMemory());
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
	}

	internal static CsvMetadata BuildSaveMetadata(string csvBody)
	{
		var dataRowCount = CountDataRows(csvBody);

		return new CsvMetadata(
			Rows: dataRowCount,
			Extras: ImmutableDictionary<string, string>.Empty
				.Add("ExportedAtUtc", DateTime.UtcNow.ToString("O")));
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
}
