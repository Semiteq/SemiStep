using Config.Dto;

using FluentResults;

using TypesShared.Results;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Config.Loaders;

internal static class ActionsSectionLoader
{
	private static readonly IDeserializer _deserializer = new DeserializerBuilder()
		.WithNamingConvention(UnderscoredNamingConvention.Instance)
		.IgnoreUnmatchedProperties()
		.Build();

	public static async Task<Result<List<ActionDto>>> LoadAsync(string configDirectory)
	{
		var actionsDirectory = Path.Combine(configDirectory, "actions");

		if (!Directory.Exists(actionsDirectory))
		{
			return Result.Fail($"Actions directory not found: {actionsDirectory}");
		}

		var yamlFiles = Directory.GetFiles(actionsDirectory, "*.yaml")
			.OrderBy(file => file)
			.ToList();

		if (yamlFiles.Count == 0)
		{
			return Result.Fail($"No YAML files found in actions directory: {actionsDirectory}");
		}

		var seenActionIds = new HashSet<short>();
		var fileResults = new List<Result<List<ActionDto>>>();

		foreach (var file in yamlFiles)
		{
			fileResults.Add(await LoadFileActionsAsync(file, seenActionIds));
		}

		var merged = Result.Merge(fileResults.ToArray());
		if (merged.IsFailed)
		{
			return merged.ToResult<List<ActionDto>>();
		}

		var allActions = fileResults
			.Where(r => r.IsSuccess)
			.SelectMany(r => r.Value)
			.ToList();

		return Result.Ok(allActions).WithReasons(merged.Reasons);
	}

	private static async Task<Result<List<ActionDto>>> LoadFileActionsAsync(
		string filePath,
		HashSet<short> seenActionIds)
	{
		try
		{
			var fileContent = await File.ReadAllTextAsync(filePath);

			var actionsDictionary =
				_deserializer.Deserialize<Dictionary<short, ActionDto>>(fileContent);

			if (actionsDictionary is null || actionsDictionary.Count == 0)
			{
				return Result.Ok(new List<ActionDto>())
					.WithWarning($"Empty or invalid YAML file: {Path.GetFileName(filePath)}");
			}

			return ValidateActionsFromFile(actionsDictionary, filePath, seenActionIds);
		}
		catch (Exception exception)
		{
			return Result.Fail<List<ActionDto>>(
				$"Failed to parse {Path.GetFileName(filePath)}: {exception.Message}");
		}
	}

	private static Result<List<ActionDto>> ValidateActionsFromFile(
		Dictionary<short, ActionDto> actionsDictionary,
		string filePath,
		HashSet<short> seenActionIds)
	{
		var actions = new List<ActionDto>();
		var validationResults = new List<Result>();

		foreach (var (id, actionContent) in actionsDictionary)
		{
			var action = CreateAction(id, actionContent);
			var validationResult = ValidateAction(action, filePath, seenActionIds);

			validationResults.Add(validationResult);

			if (validationResult.IsSuccess)
			{
				actions.Add(action);
			}
		}

		var merged = Result.Merge(validationResults.ToArray());
		if (merged.IsFailed)
		{
			return merged.ToResult<List<ActionDto>>();
		}

		return Result.Ok(actions).WithReasons(merged.Reasons);
	}

	private static ActionDto CreateAction(short id, ActionDto source)
	{
		return new ActionDto
		{
			Id = id,
			UiName = source.UiName,
			DeployDuration = source.DeployDuration,
			Columns = source.Columns
		};
	}

	private static Result ValidateAction(
		ActionDto action,
		string filePath,
		HashSet<short> seenActionIds)
	{
		var location = $"{Path.GetFileName(filePath)}, Id={action.Id}";

		var idResult = ValidateActionId(action.Id, location, seenActionIds);
		var nameResult = ValidateUiName(action.UiName, location);
		var durationResult = ValidateDeployDuration(action.DeployDuration, location);
		var columnsResult = ValidateColumns(action, location);

		return Result.Merge(idResult, nameResult, durationResult, columnsResult);
	}

	private static Result ValidateActionId(
		short actionId,
		string location,
		HashSet<short> seenActionIds)
	{
		if (actionId <= 0)
		{
			return Result.Fail($"[{location}] Action Id must be positive, got: {actionId}");
		}

		if (!seenActionIds.Add(actionId))
		{
			return Result.Fail($"[{location}] Duplicate action Id: {actionId}");
		}

		return Result.Ok();
	}

	private static Result ValidateUiName(string? uiName, string location)
	{
		if (string.IsNullOrWhiteSpace(uiName))
		{
			return Result.Fail($"[{location}] Action UiName is required");
		}

		return Result.Ok();
	}

	private static Result ValidateDeployDuration(string? deployDuration, string location)
	{
		if (string.IsNullOrWhiteSpace(deployDuration))
		{
			return Result.Fail($"[{location}] Action DeployDuration is required");
		}

		if (deployDuration is not ("immediate" or "longlasting"))
		{
			return Result.Fail($"[{location}] Action DeployDuration must be 'immediate' or 'longlasting', got: '{deployDuration}'");
		}

		return Result.Ok();
	}

	private static Result ValidateColumns(ActionDto action, string location)
	{
		if (action.Columns == null || action.Columns.Count == 0)
		{
			return Result.Ok().WithWarning($"[{location}] Action has no columns defined");
		}

		var columnResults = new List<Result>();

		foreach (var column in action.Columns)
		{
			columnResults.Add(ValidateActionColumnKey(column.Key, location));
			columnResults.Add(ValidatePropertyTypeId(column.PropertyTypeId, location));
		}

		return Result.Merge(columnResults.ToArray());
	}

	private static Result ValidatePropertyTypeId(string? propertyTypeId, string location)
	{
		if (string.IsNullOrWhiteSpace(propertyTypeId))
		{
			return Result.Fail($"[{location}] Action column PropertyTypeId is required");
		}

		return Result.Ok();
	}

	private static Result ValidateActionColumnKey(string? actionColumnKey, string location)
	{
		if (string.IsNullOrWhiteSpace(actionColumnKey))
		{
			return Result.Fail($"[{location}] Action column Key is required");
		}

		return Result.Ok();
	}
}
