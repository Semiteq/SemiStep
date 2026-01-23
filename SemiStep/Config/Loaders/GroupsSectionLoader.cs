using Config.Models;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Config.Loaders;

public sealed class GroupsSectionLoader : ISectionLoader<Dictionary<string, Dictionary<int, string>>>
{
	private readonly IDeserializer _deserializer = new DeserializerBuilder()
		.WithNamingConvention(UnderscoredNamingConvention.Instance)
		.Build();

	public async Task<Dictionary<string, Dictionary<int, string>>> LoadAsync(
		string configDirectory,
		ConfigContext context)
	{
		var groupsDir = Path.Combine(configDirectory, "groups");

		if (!Directory.Exists(groupsDir))
		{
			context.AddError($"Groups directory not found: {groupsDir}");
			return new Dictionary<string, Dictionary<int, string>>();
		}

		var yamlFiles = Directory.GetFiles(groupsDir, "*.yaml")
			.OrderBy(f => f)
			.ToList();

		if (yamlFiles.Count == 0)
		{
			context.AddError($"No YAML files found in groups directory: {groupsDir}");
			return new Dictionary<string, Dictionary<int, string>>();
		}

		var allGroups = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

		foreach (var file in yamlFiles)
		{
			try
			{
				var content = await File.ReadAllTextAsync(file);
				var fileGroups = _deserializer.Deserialize<Dictionary<string, Dictionary<int, string>>>(content);

				if (fileGroups == null)
				{
					context.AddWarning($"Empty or invalid YAML file: {Path.GetFileName(file)}", file);
					continue;
				}

				foreach (var (groupId, items) in fileGroups)
				{
					if (allGroups.ContainsKey(groupId))
					{
						context.AddError($"Duplicate group_id '{groupId}'", file);
						continue;
					}

					var validItems = items
						.Where(kv => !string.IsNullOrEmpty(kv.Value))
						.ToDictionary(kv => kv.Key, kv => kv.Value);

					allGroups[groupId] = validItems;
				}
			}
			catch (Exception ex)
			{
				context.AddError($"Failed to parse {Path.GetFileName(file)}: {ex.Message}", file);
			}
		}

		return allGroups;
	}
}
