using FluentResults;

using SemiStep.Core.Configuration.Dto;
using SemiStep.Core.Shared;

namespace SemiStep.Core.Configuration.Loaders;

internal static class ActionsSectionLoader
{
	public static Task<Result<List<ActionDto>>> LoadAsync(string configDirectory)
	{
		return MergeSectionLoader.LoadAsync<int, ActionDto, ActionDto>(
			configDirectory,
			ConfigFolderPolicy.ActionsFolder,
			EqualityComparer<int>.Default,
			MapAction);
	}

	private static Result<ActionDto> MapAction(int id, ActionDto actionContent, string fileName)
	{
		var action = CreateAction(id, actionContent);
		return ValidateAction(action, fileName).ToResult(action);
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
		string fileName)
	{
		var location = $"{fileName}, Id={action.Id}";

		var idResult = ValidateActionId(action.Id, location);
		var nameResult = ValidateUiName(action.UiName, location);
		var durationResult = ValidateDeployDuration(action.DeployDuration, location);
		var columnsResult = ValidateColumns(action, location);

		return Result.Merge(idResult, nameResult, durationResult, columnsResult);
	}

	private static Result ValidateActionId(int actionId, string location)
	{
		return actionId <= 0
			? Result.Fail($"[{location}] Action Id must be positive, got: {actionId}")
			: Result.Ok();
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
