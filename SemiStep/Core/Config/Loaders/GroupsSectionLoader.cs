using FluentResults;

using TypesShared.Results;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Config.Loaders;

internal static class GroupsSectionLoader
{
	private static readonly IDeserializer _deserializer = new DeserializerBuilder()
		.WithNamingConvention(UnderscoredNamingConvention.Instance)
		.IgnoreUnmatchedProperties()
		.Build();

	public static async Task<Result<Dictionary<string, Dictionary<int, string>>>> LoadAsync(
		string configDirectory)
	{
		var groupsDir = Path.Combine(configDirectory, "groups");

		if (!Directory.Exists(groupsDir))
		{
			return Result.Fail($"Groups directory not found: {groupsDir}");
		}

		var yamlFiles = Directory.GetFiles(groupsDir, "*.yaml")
			.OrderBy(f => f)
			.ToList();

		if (yamlFiles.Count == 0)
		{
			return Result.Fail($"No YAML files found in groups directory: {groupsDir}");
		}

		var allGroups = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
		var fileResults = new List<Result>();

		foreach (var file in yamlFiles)
		{
			fileResults.Add(await LoadFileGroupsAsync(file, allGroups));
		}

		var merged = Result.Merge(fileResults.ToArray());
		if (merged.IsFailed)
		{
			return merged.ToResult<Dictionary<string, Dictionary<int, string>>>();
		}

		return Result.Ok(allGroups).WithReasons(merged.Reasons);
	}

	private static async Task<Result> LoadFileGroupsAsync(
		string filePath,
		Dictionary<string, Dictionary<int, string>> allGroups)
	{
		try
		{
			var content = await File.ReadAllTextAsync(filePath);
			var fileGroups = _deserializer.Deserialize<Dictionary<string, Dictionary<int, string>>>(content);

			if (fileGroups is null || fileGroups.Count == 0)
			{
				return Result.Ok()
					.WithWarning($"Empty or invalid YAML file: {Path.GetFileName(filePath)}");
			}

			var validationResults = new List<Result>();

			foreach (var (groupId, items) in fileGroups)
			{
				if (allGroups.ContainsKey(groupId))
				{
					validationResults.Add(
						Result.Fail($"[{Path.GetFileName(filePath)}] Duplicate group_id '{groupId}'"));
					continue;
				}

				var validItems = items
					.Where(kv => !string.IsNullOrEmpty(kv.Value))
					.ToDictionary(kv => kv.Key, kv => kv.Value);

				allGroups[groupId] = validItems;
			}

			if (validationResults.Count == 0)
			{
				return Result.Ok();
			}

			return Result.Merge(validationResults.ToArray());
		}
		catch (Exception ex)
		{
			return Result.Fail(
				$"Failed to parse {Path.GetFileName(filePath)}: {ex.Message}");
		}
	}
}
