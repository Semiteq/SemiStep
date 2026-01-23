using Config.Dto;
using Config.Models;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Config.Loaders;

public sealed class ActionsSectionLoader : ISectionLoader<List<ActionDto>>
{
	private readonly IDeserializer _deserializer = new DeserializerBuilder()
		.WithNamingConvention(UnderscoredNamingConvention.Instance)
		.Build();

	public async Task<List<ActionDto>> LoadAsync(string configDirectory, ConfigContext context)
	{
		var actionsDir = Path.Combine(configDirectory, "actions");

		if (!Directory.Exists(actionsDir))
		{
			context.AddError($"Actions directory not found: {actionsDir}");
			return [];
		}

		var yamlFiles = Directory.GetFiles(actionsDir, "*.yaml")
			.OrderBy(f => f)
			.ToList();

		if (yamlFiles.Count == 0)
		{
			context.AddError($"No YAML files found in actions directory: {actionsDir}");
			return [];
		}

		var allActions = new List<ActionDto>();
		var seenIds = new HashSet<short>();

		foreach (var file in yamlFiles)
		{
			try
			{
				var content = await File.ReadAllTextAsync(file);
				var actions = _deserializer.Deserialize<List<ActionDto>>(content);

				if (actions == null)
				{
					context.AddWarning($"Empty or invalid YAML file: {Path.GetFileName(file)}", file);
					continue;
				}

				foreach (var action in actions)
				{
					ValidateAction(action, file, context, seenIds);
				}

				allActions.AddRange(actions);
			}
			catch (Exception ex)
			{
				context.AddError($"Failed to parse {Path.GetFileName(file)}: {ex.Message}", file);
			}
		}

		return allActions;
	}

	private static void ValidateAction(ActionDto action, string file, ConfigContext context, HashSet<short> seenIds)
	{
		var fileName = Path.GetFileName(file);
		var location = $"{fileName}, Id={action.Id}";

		if (action.Id <= 0)
		{
			context.AddError($"Action Id must be positive, got: {action.Id}", location);
		}
		else if (!seenIds.Add(action.Id))
		{
			context.AddError($"Duplicate action Id: {action.Id}", location);
		}

		if (string.IsNullOrWhiteSpace(action.UiName))
		{
			context.AddError("Action UiName is required", location);
		}

		if (string.IsNullOrWhiteSpace(action.DeployDuration))
		{
			context.AddError("Action DeployDuration is required", location);
		}
		else if (action.DeployDuration != "immediate" && action.DeployDuration != "longlasting")
		{
			context.AddError(
				$"Action DeployDuration must be 'immediate' or 'longlasting', got: '{action.DeployDuration}'",
				location);
		}

		if (action.Columns == null || action.Columns.Count == 0)
		{
			context.AddWarning("Action has no columns defined", location);
		}
		else
		{
			foreach (var column in action.Columns)
			{
				ValidateActionColumn(column, location, context);
			}
		}
	}

	private static void ValidateActionColumn(ActionColumnDto column, string actionLocation, ConfigContext context)
	{
		if (string.IsNullOrWhiteSpace(column.Key))
		{
			context.AddError("Action column Key is required", actionLocation);
		}

		if (string.IsNullOrWhiteSpace(column.PropertyTypeId))
		{
			context.AddError($"Action column '{column.Key}' PropertyTypeId is required", actionLocation);
		}
	}
}
