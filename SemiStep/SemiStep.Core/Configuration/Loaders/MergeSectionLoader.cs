using FluentResults;

using SemiStep.Core.Shared;

namespace SemiStep.Core.Configuration.Loaders;

internal static class MergeSectionLoader
{
	public static async Task<Result<List<TItem>>> LoadAsync<TKey, TValue, TItem>(
		string configDirectory,
		string folderName,
		IEqualityComparer<TKey> keyComparer,
		Func<TKey, TValue, string, Result<TItem>> mapEntry)
		where TKey : notnull
	{
		var sectionDirectory = Path.Combine(configDirectory, folderName);
		var displayName = char.ToUpperInvariant(folderName[0]) + folderName[1..];

		if (!Directory.Exists(sectionDirectory))
		{
			return Result.Fail($"{displayName} directory not found: {sectionDirectory}");
		}

		var yamlFiles = ConfigFolderPolicy.GetYamlFilesToRead(sectionDirectory);

		if (yamlFiles.Count == 0)
		{
			return Result.Fail($"No YAML files found in {folderName} directory: {sectionDirectory}");
		}

		var fileResults = new List<Result<List<TItem>>>();
		var firstFileByKey = new Dictionary<TKey, string>(keyComparer);

		foreach (var file in yamlFiles)
		{
			fileResults.Add(await LoadFileAsync(file, firstFileByKey, mapEntry));
		}

		return Result.Merge([.. fileResults])
			.Map(fileItems => fileItems.SelectMany(items => items).ToList());
	}

	private static async Task<Result<List<TItem>>> LoadFileAsync<TKey, TValue, TItem>(
		string filePath,
		Dictionary<TKey, string> firstFileByKey,
		Func<TKey, TValue, string, Result<TItem>> mapEntry)
		where TKey : notnull
	{
		var fileName = Path.GetFileName(filePath);

		try
		{
			var content = await File.ReadAllTextAsync(filePath);
			var parsed = YamlConfigDeserializer.Instance.Deserialize<Dictionary<TKey, TValue>>(content);

			if (parsed is null || parsed.Count == 0)
			{
				return Result.Ok(new List<TItem>())
					.WithWarning($"Empty or invalid YAML file: {fileName}");
			}

			var entryResults = new List<Result<TItem>>();

			foreach (var (key, value) in parsed)
			{
				entryResults.Add(LoadEntry(key, value, fileName, firstFileByKey, mapEntry));
			}

			return Result.Merge([.. entryResults]).Map(items => items.ToList());
		}
		catch (Exception ex)
		{
			return Result.Fail($"Failed to parse {fileName}: {ex.Message}");
		}
	}

	private static Result<TItem> LoadEntry<TKey, TValue, TItem>(
		TKey key,
		TValue value,
		string fileName,
		Dictionary<TKey, string> firstFileByKey,
		Func<TKey, TValue, string, Result<TItem>> mapEntry)
		where TKey : notnull
	{
		if (firstFileByKey.TryGetValue(key, out var firstFileName))
		{
			return Result.Fail(new DuplicateKeyError($"{key}", firstFileName, fileName));
		}

		firstFileByKey[key] = fileName;

		return value is null
			? Result.Fail($"[{fileName}] Key '{key}' has no value")
			: mapEntry(key, value, fileName);
	}
}
