using Config.Loaders;
using Config.Mapping;
using Config.Validation;

using FluentResults;

using Serilog;

using TypesShared.Config;
using TypesShared.Results;

namespace Config.Facade;

public static class ConfigFacade
{
	public static async Task<Result<AppConfiguration>> LoadAndValidateAsync(string configDirectory)
	{
		if (!Directory.Exists(configDirectory))
		{
			Log.Error("Configuration directory not found: {ConfigDirectory}", configDirectory);

			return Result.Fail($"Configuration directory not found: {configDirectory}");
		}

		var loadResult = await LoadAllSectionsAsync(configDirectory);
		if (loadResult.IsFailed)
		{
			foreach (var error in loadResult.Errors)
			{
				Log.Error("Configuration error: {Error}", error.Message);
			}

			return loadResult.ToResult<AppConfiguration>();
		}

		var (properties, columns, groups, actions, gridStyle, connection) = loadResult.Value;

		var xrefResult = CrossReferenceValidator.Validate(properties, columns, groups, actions);
		if (xrefResult.IsFailed)
		{
			foreach (var error in xrefResult.Errors)
			{
				Log.Error("Configuration error: {Error}", error.Message);
			}

			return Result.Fail<AppConfiguration>(xrefResult.Errors)
				.WithReasons(loadResult.Reasons)
				.WithReasons(xrefResult.Successes.OfType<Warning>());
		}

		var defaultsResult = DefaultValueValidator.Validate(properties, columns, actions);
		if (defaultsResult.IsFailed)
		{
			foreach (var error in defaultsResult.Errors)
			{
				Log.Error("Configuration error: {Error}", error.Message);
			}

			return Result.Fail<AppConfiguration>(defaultsResult.Errors)
				.WithReasons(loadResult.Reasons)
				.WithReasons(xrefResult.Reasons)
				.WithReasons(defaultsResult.Successes.OfType<Warning>());
		}

		try
		{
			var config = MapToDomain(properties, columns, groups, actions, gridStyle, connection);
			Log.Information("Configuration loaded successfully");

			return Result.Ok(config)
				.WithReasons(loadResult.Reasons)
				.WithReasons(xrefResult.Reasons)
				.WithReasons(defaultsResult.Reasons);
		}
		catch (Exception ex)
		{
			Log.Error("Failed to map configuration to domain: {message}", ex.Message);

			return Result.Fail<AppConfiguration>($"Failed to map configuration to domain: {ex.Message}");
		}
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

		var mappedGroups = GroupMapper.Map(groups);

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
