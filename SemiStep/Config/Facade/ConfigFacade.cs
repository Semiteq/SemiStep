using Config.Loaders;
using Config.Mapping;
using Config.Models;
using Config.Validation;

using Serilog;

using Shared;

namespace Config.Facade;

public sealed class ConfigFacade(ILogger? logger = null)
{
	private readonly ActionsSectionLoader _actionsLoader = new();
	private readonly ColumnsSectionLoader _columnsLoader = new();
	private readonly PropertiesSectionLoader _propertiesLoader = new();
	private readonly GroupsSectionLoader _groupsLoader = new();
	private readonly GridStyleLoader _gridStyleLoader = new();
	private readonly ActionMapper _actionMapper = new();
	private readonly ColumnMapper _columnMapper = new();
	private readonly PropertyMapper _propertyMapper = new();
	private readonly GroupMapper _groupMapper = new();
	private readonly GridStyleMapper _gridStyleMapper = new();

	public async Task<ConfigContext> LoadAsync(string configDirectory)
	{
		var context = new ConfigContext
		{
			FilePaths = [configDirectory]
		};

		if (!Directory.Exists(configDirectory))
		{
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
			context.AddError($"Failed to map configuration to domain: {ex.Message}");
		}

		return context;
	}

	private async Task<ConfigContext> LoadAllSectionsAsync(string configDirectory, ConfigContext context)
	{
		context.Properties = await _propertiesLoader.LoadAsync(configDirectory, context);
		context.Columns = await _columnsLoader.LoadAsync(configDirectory, context);
		context.Groups = await _groupsLoader.LoadAsync(configDirectory, context);
		context.Actions = await _actionsLoader.LoadAsync(configDirectory, context);
		context.GridStyle = await _gridStyleLoader.LoadAsync(configDirectory, context);

		return context;
	}

	private AppConfiguration MapToDomain(ConfigContext context)
	{
		var properties = _propertyMapper
			.MapMany(context.Properties!)
			.ToDictionary(p => p.PropertyTypeId, StringComparer.OrdinalIgnoreCase);

		var columns = _columnMapper
			.MapMany(context.Columns!)
			.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);

		var groups = _groupMapper.Map(context.Groups!);

		var actions = _actionMapper
			.MapMany(context.Actions!)
			.ToDictionary(a => a.Id);

		var gridStyle = _gridStyleMapper.Map(context.GridStyle);

		return new AppConfiguration(properties, columns, groups, actions, gridStyle);
	}
}
