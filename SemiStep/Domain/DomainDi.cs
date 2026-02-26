using Domain.Facade;
using Domain.Ports;
using Domain.Registries;
using Domain.Services;
using Domain.State;

using Microsoft.Extensions.DependencyInjection;

using Serilog;

using Shared.Registries;

namespace Domain;

public static class DomainDi
{
	public static IServiceCollection AddDomain(this IServiceCollection services)
	{
		services.AddSingleton<IActionRegistry, ActionRegistry>();
		services.AddSingleton<IPropertyRegistry, PropertyRegistry>();
		services.AddSingleton<IColumnRegistry, ColumnRegistry>();
		services.AddSingleton<IGroupRegistry, GroupRegistry>();
		services.AddSingleton<CellStateResolver>();
		services.AddSingleton<RecipeStateManager>();
		services.AddSingleton<RecipeHistoryManager>();
		services.AddSingleton<CoreService>();
		services.AddSingleton<DomainFacade>();

		return services;
	}
}
