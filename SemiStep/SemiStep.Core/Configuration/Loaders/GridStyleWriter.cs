using System.Text;

using FluentResults;

using SemiStep.Core.Configuration.Mapping;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SemiStep.Core.Configuration.Loaders;

internal sealed class GridStyleWriter
{
	private static readonly ISerializer _serializer = new SerializerBuilder()
		.WithNamingConvention(UnderscoredNamingConvention.Instance)
		.Build();

	private static readonly UTF8Encoding _utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

	public async Task<Result> SaveAsync(string configDirectory, GridStyleOptions options)
	{
		var uiDir = Path.Combine(configDirectory, "ui");
		var filePath = Path.Combine(uiDir, "grid_style.yaml");

		try
		{
			Directory.CreateDirectory(uiDir);

			var header = await ReadLeadingCommentBlockAsync(filePath);
			var body = _serializer.Serialize(GridStyleDtoMapper.Map(options));
			var content = Normalize(header + body);

			await WriteAtomicAsync(uiDir, filePath, content);

			return Result.Ok();
		}
		catch (Exception ex)
		{
			return Result.Fail(new GridStyleSaveFailedError(Path.GetFileName(filePath)).CausedBy(ex));
		}
	}

	private static async Task<string> ReadLeadingCommentBlockAsync(string filePath)
	{
		if (!File.Exists(filePath))
		{
			return string.Empty;
		}

		var lines = await File.ReadAllLinesAsync(filePath);
		var headerLines = new List<string>();

		foreach (var line in lines)
		{
			var trimmed = line.TrimStart();
			if (trimmed.Length == 0 || trimmed.StartsWith('#'))
			{
				headerLines.Add(line);
			}
			else
			{
				break;
			}
		}

		if (headerLines.Count == 0)
		{
			return string.Empty;
		}

		return string.Join('\n', headerLines).TrimEnd('\n') + "\n\n";
	}

	private static string Normalize(string content)
	{
		return content.Replace("\r\n", "\n").Replace("\r", "\n");
	}

	private static async Task WriteAtomicAsync(string uiDir, string filePath, string content)
	{
		var tempPath = Path.Combine(uiDir, $".grid_style.{Guid.NewGuid():N}.tmp");

		await File.WriteAllTextAsync(tempPath, content, _utf8NoBom);

		try
		{
			File.Move(tempPath, filePath, overwrite: true);
		}
		catch
		{
			TryDelete(tempPath);
			throw;
		}
	}

	private static void TryDelete(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch
		{
		}
	}
}
