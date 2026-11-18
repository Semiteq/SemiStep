using FluentResults;

using SemiStep.Core.Configuration.Dto;
using SemiStep.Core.Shared;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SemiStep.Core.Configuration.Loaders;

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

		var seenActionIds = new HashSet<int>();
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
		HashSet<int> seenActionIds)
	{
		try
		{
			var fileContent = await File.ReadAllTextAsync(filePath);

			var actionsDictionary =
				_deserializer.Deserialize<Dictionary<int, ActionDto>>(fileContent);

			return actionsDictionary is null || actionsDictionary.Count == 0
				? Result.Ok(new List<ActionDto>())
					.WithWarning($"Empty or invalid YAML file: {Path.GetFileName(filePath)}")
				: ValidateActionsFromFile(actionsDictionary, filePath, seenActionIds);
		}
		catch (Exception exception)
		{
			return Result.Fail(
				$"Failed to parse {Path.GetFileName(filePath)}: {exception.Message}");
		}
	}

	private static Result<List<ActionDto>> ValidateActionsFromFile(
		Dictionary<int, ActionDto> actionsDictionary,
		string filePath,
		HashSet<int> seenActionIds)
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

		var merged = Result.Merge([.. validationResults]);
		return merged.IsFailed ? merged.ToResult<List<ActionDto>>() : Result.Ok(actions).WithReasons(merged.Reasons);
	}

	private static ActionDto CreateAction(int id, ActionDto source)
	{
		return new ActionDto
		{
			Id = id,
			UiName = source.UiName,
			Role = source.Role,
			DeployDuration = source.DeployDuration,
			Columns = source.Columns,
			Formula = source.Formula
		};
	}

	private static Result ValidateAction(
		ActionDto action,
		string filePath,
		HashSet<int> seenActionIds)
	{
		var location = $"{Path.GetFileName(filePath)}, Id={action.Id}";

		var idResult = ValidateActionId(action.Id, location, seenActionIds);
		var nameResult = ValidateUiName(action.UiName, location);
		var durationResult = ValidateDeployDuration(action.DeployDuration, location);
		var columnsResult = ValidateColumns(action, location);

		return Result.Merge(idResult, nameResult, durationResult, columnsResult);
	}

	private static Result ValidateActionId(
		int actionId,
		string location,
		HashSet<int> seenActionIds)
	{
		return actionId <= 0
			? Result.Fail($"[{location}] Action Id must be positive, got: {actionId}")
			: !seenActionIds.Add(actionId) ? Result.Fail($"[{location}] Duplicate action Id: {actionId}") : Result.Ok();
	}

	private static Result ValidateUiName(string? uiName, string location)
	{
		return string.IsNullOrWhiteSpace(uiName) ? Result.Fail($"[{location}] Action UiName is required") : Result.Ok();
	}

	private static Result ValidateDeployDuration(string? deployDuration, string location)
	{
		return string.IsNullOrWhiteSpace(deployDuration)
			? Result.Fail($"[{location}] Action DeployDuration is required")
			: deployDuration is not ("immediate" or "longlasting")
			? Result.Fail($"[{location}] Action DeployDuration must be 'immediate' or 'longlasting', got: '{deployDuration}'")
			: Result.Ok();
	}

	private static Result ValidateColumns(ActionDto action, string location)
	{
		if (action.Columns is null || action.Columns.Count == 0)
		{
			return Result.Ok().WithWarning($"[{location}] Action has no columns defined");
		}

		var columnResults = new List<Result>();

		foreach (var column in action.Columns)
		{
			columnResults.Add(ValidateActionColumnKey(column.Key, location));
			columnResults.Add(ValidatePropertyTypeId(column.PropertyTypeId, location));
		}

		return Result.Merge([.. columnResults]);
	}

	private static Result ValidatePropertyTypeId(string? propertyTypeId, string location)
	{
		return string.IsNullOrWhiteSpace(propertyTypeId)
			? Result.Fail($"[{location}] Action column PropertyTypeId is required")
			: Result.Ok();
	}

	private static Result ValidateActionColumnKey(string? actionColumnKey, string location)
	{
		return string.IsNullOrWhiteSpace(actionColumnKey) ? Result.Fail($"[{location}] Action column Key is required") : Result.Ok();
	}
}
