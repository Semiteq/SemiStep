using Config.Loaders;
using Config.Mapping;
using Config.Models;
using Config.Validation;

using Serilog;
using Serilog.Core;

using Shared;

namespace Config.Facade;

public sealed class ConfigFacade(Logger logger)
{
	public async Task<ConfigContext> LoadAsync(string configDirectory)
	{
		var context = new ConfigContext { FilePaths = [configDirectory] };

		if (!Directory.Exists(configDirectory))
		{
			context.AddError($"Configuration directory not found: {configDirectory}");
			logger?.Error("Configuration directory not found: {ConfigDirectory}", configDirectory);

			return context;
		}

		context = await LoadAllSectionsAsync(configDirectory, context);

		if (context.HasErrors)
		{
			return context;
		}

		context = CrossReferenceValidator.Validate(context);

		if (context.HasErrors)
		{
			return context;
		}

		try
		{
			context.Configuration = MapToDomain(context);
		}
		catch (Exception ex)
		{
			logger?.Error("Failed to map configuration to domain: {message}", ex.Message);
			context.AddError($"Failed to map configuration to domain: {ex.Message}");
		}

		return context;
	}

	private static async Task<ConfigContext> LoadAllSectionsAsync(string configDirectory, ConfigContext context)
	{
		context.Properties = await PropertiesSectionLoader.LoadAsync(configDirectory, context);
		context.Columns = await ColumnsSectionLoader.LoadAsync(configDirectory, context);
		context.Groups = await GroupsSectionLoader.LoadAsync(configDirectory, context);
		context.Actions = await ActionsSectionLoader.LoadAsync(configDirectory, context);
		context.GridStyle = await GridStyleLoader.LoadAsync(configDirectory, context);
		context.Connection = await ConnectionLoader.LoadAsync(configDirectory, context);

		return context;
	}

	private static AppConfiguration MapToDomain(ConfigContext context)
	{
		var properties = PropertyMapper.MapMany(context.Properties!)
			.ToDictionary(p => p.PropertyTypeId, StringComparer.OrdinalIgnoreCase);

		var columns = ColumnMapper.MapMany(context.Columns!)
			.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);

		var groups = GroupMapper.Map(context.Groups!);

		var actions = ActionMapper.MapMany(context.Actions!)
			.ToDictionary(a => a.Id);

		var gridStyle = GridStyleMapper.Map(context.GridStyle);

		var plcConfiguration = ConnectionMapper.Map(context.Connection);

		return new AppConfiguration(properties, columns, groups, actions, gridStyle, plcConfiguration);
	}
}
