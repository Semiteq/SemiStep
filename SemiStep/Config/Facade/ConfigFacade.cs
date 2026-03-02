using Config.Loaders;
using Config.Mapping;
using Config.Models;
using Config.Validation;

using Serilog;

using Shared;
using Shared.Reasons;

namespace Config.Facade;

public sealed class ConfigFacade
{
	public static async Task<AppConfiguration> LoadAndValidateAsync(string configDirectory)
	{
		var context = await LoadAsync(configDirectory);

		if (context.HasErrors)
		{
			foreach (var error in context.Errors)
			{
				var location = error is ConfigLoadError configError ? configError.Location : null;
				Log.Error("Configuration error: {Message} at {Location}", error.Message, location ?? "unknown");
			}

			throw new InvalidOperationException("Configuration loading failed with errors.");
		}

		if (context.Configuration is null)
		{
			Log.Error("Configuration is null after successful loading");
			throw new InvalidOperationException("Configuration is null after successful loading.");
		}

		Log.Information("Configuration loaded successfully");

		return context.Configuration;
	}

	public static async Task<ConfigContext> LoadAsync(string configDirectory)
	{
		var context = new ConfigContext { FilePaths = [configDirectory] };

		if (!Directory.Exists(configDirectory))
		{
			context.AddError($"Configuration directory not found", configDirectory);
			Log.Error("Configuration directory not found: {ConfigDirectory}", configDirectory);

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
			Log.Error("Failed to map configuration to domain: {message}", ex.Message);
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
