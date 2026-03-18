using Domain.Facade;
using Domain.Registries;
using Domain.Services;
using Domain.State;

using Microsoft.Extensions.DependencyInjection;

using Shared.Config;
using Shared.Config.Contracts;
using Shared.ServiceContracts;

namespace Domain;

public static class DomainDi
{
	public static IServiceCollection AddDomain(this IServiceCollection services)
	{
		services.AddSingleton<IActionRegistry, ActionRegistry>();
		services.AddSingleton<IPropertyRegistry, PropertyRegistry>();
		services.AddSingleton<IColumnRegistry, ColumnRegistry>();
		services.AddSingleton<IGroupRegistry, GroupRegistry>();
		services.AddSingleton<RecipeStateManager>();
		services.AddSingleton<RecipeHistoryManager>();
		services.AddSingleton<CoreService>();
		services.AddSingleton<DomainFacade>(
			sp => new DomainFacade(
				sp.GetRequiredService<AppConfiguration>(),
				sp.GetRequiredService<IActionRegistry>(),
				sp.GetRequiredService<IPropertyRegistry>(),
				sp.GetRequiredService<IColumnRegistry>(),
				sp.GetRequiredService<IGroupRegistry>(),
				sp.GetRequiredService<CoreService>(),
				sp.GetRequiredService<RecipeStateManager>(),
				sp.GetRequiredService<RecipeHistoryManager>(),
				sp.GetRequiredService<ICsvService>(),
				sp.GetRequiredService<IS7ConnectionService>(),
				sp.GetRequiredService<ICsvClipboardService>())
			);

		return services;
	}
}
