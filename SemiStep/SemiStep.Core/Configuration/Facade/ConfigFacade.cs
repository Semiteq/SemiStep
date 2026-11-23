using FluentResults;

using SemiStep.Core.Configuration.Loaders;
using SemiStep.Core.Configuration.Mapping;
using SemiStep.Core.Configuration.Validation;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Recipes;

namespace SemiStep.Core.Configuration.Facade;

public static class ConfigFacade
{
	public static async Task<Result<AppConfiguration>> LoadAndValidateAsync(string configDirectory)
	{
		if (!Directory.Exists(configDirectory))
		{
			return Result.Fail($"Configuration directory not found: {configDirectory}");
		}

		var loadResult = await LoadAllSectionsAsync(configDirectory);
		if (loadResult.IsFailed)
		{
			return Propagate(loadResult);
		}

		var (properties, columns, groups, actions, gridStyle, connection, appUi) = loadResult.Value;

		var gridStyleResult = GridStyleMapper.Map(gridStyle);
		if (gridStyleResult.IsFailed)
		{
			return Propagate(gridStyleResult, loadResult);
		}

		var xrefResult = CrossReferenceValidator.Validate(properties, columns, groups, actions);
		if (xrefResult.IsFailed)
		{
			return Propagate(xrefResult, loadResult);
		}

		var defaultsResult = DefaultValueValidator.Validate(properties, columns, actions);
		if (defaultsResult.IsFailed)
		{
			return Propagate(defaultsResult, loadResult, xrefResult);
		}

		var mapResult = MapToDomain(properties, columns, groups, actions, gridStyleResult.Value, connection, appUi);

		if (mapResult.IsFailed)
		{
			return Propagate(mapResult, loadResult, xrefResult, defaultsResult);
		}

		var config = mapResult.Value;

		var plcResult = PlcConfigurationValidator.Validate(config.PlcConfiguration);
		return plcResult.IsFailed
			? Propagate(plcResult, loadResult, xrefResult, defaultsResult)
			: Result.Ok(config)
			.WithReasons(loadResult.Reasons)
			.WithReasons(xrefResult.Reasons)
			.WithReasons(defaultsResult.Reasons);
	}

	private static Result<AppConfiguration> Propagate(ResultBase failedResult, params ResultBase[] priorReasons)
	{
		var propagated = Result.Fail<AppConfiguration>(failedResult.Errors);

		foreach (var prior in priorReasons)
		{
			propagated = propagated.WithReasons(prior.Reasons);
		}

		// Carry forward the failed step's successes (warnings, etc.); its errors are already attached above.
		return propagated.WithReasons(failedResult.Successes);
	}

	private static async Task<Result<LoadedSections>> LoadAllSectionsAsync(string configDirectory)
	{
		var propertiesResult = await PropertiesSectionLoader.LoadAsync(configDirectory);
		var columnsResult = await ColumnsSectionLoader.LoadAsync(configDirectory);
		var groupsResult = await GroupsSectionLoader.LoadAsync(configDirectory);
		var actionsResult = await ActionsSectionLoader.LoadAsync(configDirectory);
		var gridStyleResult = await GridStyleLoader.LoadAsync(configDirectory);
		var connectionResult = await ConnectionLoader.LoadAsync(configDirectory);
		var appUiResult = await AppUiOptionsLoader.LoadAsync(configDirectory);

		var merged = Result.Merge(
			ConfigFolderPolicy.Scan(configDirectory),
			propertiesResult.ToResult(),
			columnsResult.ToResult(),
			groupsResult.ToResult(),
			actionsResult.ToResult(),
			WithParserDetail(gridStyleResult.ToResult()),
			connectionResult.ToResult(),
			appUiResult.ToResult());

		if (merged.IsFailed)
		{
			return merged.ToResult<LoadedSections>();
		}

		var sections = new LoadedSections(
			propertiesResult.Value,
			columnsResult.Value,
			groupsResult.Value,
			actionsResult.Value,
			gridStyleResult.Value,
			connectionResult.Value,
			appUiResult.Value);

		return Result.Ok(sections).WithReasons(merged.Reasons);
	}

	/// <summary>The style editor localizes GridStyleLoadFailedError by type; the startup window shows Message only.</summary>
	private static Result WithParserDetail(Result gridStyleResult)
	{
		return gridStyleResult.IsSuccess
			? gridStyleResult
			: Result.Fail(gridStyleResult.Errors.Select(AppendParserDetail));
	}

	private static IError AppendParserDetail(IError error)
	{
		return error is GridStyleLoadFailedError && error.Reasons.OfType<ExceptionalError>().FirstOrDefault() is { } cause
			? new Error($"{error.Message}: {cause.Exception.Message}").CausedBy(cause.Exception)
			: error;
	}

	private static Result<AppConfiguration> MapToDomain(
		List<Dto.PropertyDto> properties,
		List<Dto.ColumnDto> columns,
		Dictionary<string, Dictionary<int, string>> groups,
		List<Dto.ActionDto> actions,
		GridStyleOptions gridStyle,
		Dto.ConnectionDto? connection,
		AppUiOptions appUiOptions)
	{
		var mappedProperties = PropertyMapper.MapMany(properties)
			.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);

		var mappedColumns = ColumnMapper.MapMany(columns)
			.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);

		var mappedGroups = new Dictionary<string, GroupDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (var (groupId, items) in groups)
		{
			mappedGroups[groupId] = new GroupDefinition(groupId, items.AsReadOnly());
		}

		var actionsResult = ActionMapper.TryMapMany(actions, mappedProperties);
		if (actionsResult.IsFailed)
		{
			return actionsResult.ToResult<AppConfiguration>();
		}

		var mappedActions = new Dictionary<int, ActionDefinition>();
		foreach (var action in actionsResult.Value)
		{
			if (mappedActions.ContainsKey(action.Id))
			{
				return Result.Fail<AppConfiguration>(
					$"Duplicate action Id '{action.Id}' detected during domain mapping.");
			}

			mappedActions.Add(action.Id, action);
		}

		var plcConfiguration = ConnectionMapper.Map(connection);

		return Result.Ok(new AppConfiguration(
			mappedProperties, mappedColumns, mappedGroups,
			mappedActions, gridStyle, plcConfiguration, appUiOptions));
	}

	private sealed record LoadedSections(
		List<Dto.PropertyDto> Properties,
		List<Dto.ColumnDto> Columns,
		Dictionary<string, Dictionary<int, string>> Groups,
		List<Dto.ActionDto> Actions,
		Dto.GridStyleOptionsDto? GridStyle,
		Dto.ConnectionDto? Connection,
		AppUiOptions AppUi);
}
