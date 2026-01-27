using Domain.Facade;
using Domain.Registries;
using Domain.Services;
using Domain.State;

using Microsoft.Extensions.DependencyInjection;

using Serilog;

using Shared.Registries;
using Shared.Services;

namespace Domain;

public static class DomainDi
{
	public static IServiceCollection AddDomain(this IServiceCollection services, ILogger? logger = null)
	{
		if (logger is not null)
		{
			services.AddSingleton(logger);
		}

		services.AddSingleton<IActionRegistry, ActionRegistry>();
		services.AddSingleton<IPropertyRegistry, PropertyRegistry>();
		services.AddSingleton<IColumnRegistry, ColumnRegistry>();
		services.AddSingleton<IGroupRegistry, GroupRegistry>();
		services.AddSingleton<IGridStyleProvider, GridStyleProvider>();
		services.AddSingleton<CellStateResolver>();
		services.AddSingleton<RecipeStateManager>();
		services.AddSingleton<RecipeService>();
		services.AddSingleton<DomainFacade>();

		return services;
	}
}
