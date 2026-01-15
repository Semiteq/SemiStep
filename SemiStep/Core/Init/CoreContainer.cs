using Core.Definitions;
using Core.Definitions.Contracts;
using Core.Domain;
using Core.Properties.Contracts;
using Core.Services;

using FluentResults;

using Microsoft.Extensions.DependencyInjection;

namespace Core.Init;

public static class CoreContainer
{
	public static IServiceCollection AddCore(this IServiceCollection services)
	{
		services.AddSingleton<RecipeImporter>();
		services.AddSingleton<ICoreFacade, CoreFacade>();

		services.AddSingleton<IActionCatalog, ActionCatalogNotConfigured>();
		services.AddSingleton<IPropertySchema, PropertySchemaNotConfigured>();

		return services;
	}

	private sealed class ActionCatalogNotConfigured : IActionCatalog
	{
		public Result<ActionDefinition> GetByKey(string actionKey) =>
			Result.Fail("IActionCatalog is not configured.");
	}

	private sealed class PropertySchemaNotConfigured : IPropertySchema
	{
		public Result<IPropertyDefinition> GetDefinition(ActionDefinition action, string columnId) =>
			Result.Fail("IPropertySchema is not configured.");
	}
}
