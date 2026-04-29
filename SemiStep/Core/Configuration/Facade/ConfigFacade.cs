using FluentResults;

using SemiStep.Core.Configuration.Loaders;
using SemiStep.Core.Configuration.Mapping;
using SemiStep.Core.Configuration.Validation;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Recipes;
using SemiStep.Core.Shared;

using Serilog;

namespace SemiStep.Core.Configuration.Facade;

public static class ConfigFacade
{
	// ConfigFacade is a static class, so it cannot be used as a type argument.
	// Log.ForContext(typeof(...)) is the supported alternative.
	private static readonly ILogger _logger = Log.ForContext(typeof(ConfigFacade));

	public static async Task<Result<AppConfiguration>> LoadAndValidateAsync(string configDirectory)
	{
		if (!Directory.Exists(configDirectory))
		{
			_logger.Error("Configuration directory not found: {ConfigDirectory}", configDirectory);

			return Result.Fail($"Configuration directory not found: {configDirectory}");
		}

		var loadResult = await LoadAllSectionsAsync(configDirectory);
		if (loadResult.IsFailed)
		{
			return LogAndPropagate(loadResult);
		}

		var (properties, columns, groups, actions, gridStyle, connection) = loadResult.Value;

		var xrefResult = CrossReferenceValidator.Validate(properties, columns, groups, actions);
		if (xrefResult.IsFailed)
		{
			return LogAndPropagate(xrefResult, loadResult);
		}

		var defaultsResult = DefaultValueValidator.Validate(properties, columns, actions);
		if (defaultsResult.IsFailed)
		{
			return LogAndPropagate(defaultsResult, loadResult, xrefResult);
		}

		var mapResult = Result.Try(
			() => MapToDomain(properties, columns, groups, actions, gridStyle, connection),
			ex => new Error("Failed to map configuration to domain: " + ex.Message).CausedBy(ex));

		if (mapResult.IsFailed)
		{
			return LogAndPropagate(mapResult, loadResult, xrefResult, defaultsResult);
		}

		var config = mapResult.Value;

		var plcResult = PlcConfigurationValidator.Validate(config.PlcConfiguration);
		if (plcResult.IsFailed)
		{
			return LogAndPropagate(plcResult, loadResult, xrefResult, defaultsResult);
		}

		_logger.Information("Configuration loaded successfully");

		return Result.Ok(config)
			.WithReasons(loadResult.Reasons)
			.WithReasons(xrefResult.Reasons)
			.WithReasons(defaultsResult.Reasons);
	}

	private static Result<AppConfiguration> LogAndPropagate(ResultBase failedResult, params ResultBase[] priorReasons)
	{
		foreach (var error in failedResult.Errors)
		{
			_logger.Error("Configuration error: {Error}", error.Message);
		}

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

		var merged = Result.Merge(
			propertiesResult.ToResult(),
			columnsResult.ToResult(),
			groupsResult.ToResult(),
			actionsResult.ToResult(),
			gridStyleResult.ToResult(),
			connectionResult.ToResult());

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
			connectionResult.Value);

		return Result.Ok(sections).WithReasons(merged.Reasons);
	}

	private static AppConfiguration MapToDomain(
		List<Dto.PropertyDto> properties,
		List<Dto.ColumnDto> columns,
		Dictionary<string, Dictionary<int, string>> groups,
		List<Dto.ActionDto> actions,
		Dto.GridStyleOptionsDto? gridStyle,
		Dto.ConnectionDto? connection)
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

		var mappedActions = ActionMapper.MapMany(actions)
			.ToDictionary(a => a.Id);

		var mappedGridStyle = GridStyleMapper.Map(gridStyle);

		var plcConfiguration = ConnectionMapper.Map(connection);

		return new AppConfiguration(
			mappedProperties, mappedColumns, mappedGroups,
			mappedActions, mappedGridStyle, plcConfiguration);
	}

	private sealed record LoadedSections(
		List<Dto.PropertyDto> Properties,
		List<Dto.ColumnDto> Columns,
		Dictionary<string, Dictionary<int, string>> Groups,
		List<Dto.ActionDto> Actions,
		Dto.GridStyleOptionsDto? GridStyle,
		Dto.ConnectionDto? Connection);
}
