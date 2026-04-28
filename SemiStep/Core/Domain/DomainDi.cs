using Domain.Facade;
using Domain.Helpers;
using Domain.State;

using Microsoft.Extensions.DependencyInjection;

using TypesShared.Config;
using TypesShared.Core;
using TypesShared.Domain;

namespace Domain;

public static class DomainDi
{
	public static IServiceCollection AddDomain(this IServiceCollection services)
	{
		services.AddSingleton(sp => new ConfigRegistry(sp.GetRequiredService<AppConfiguration>()));
		services.AddSingleton<RecipeStateManager>();
		services.AddSingleton<RecipeHistoryManager>();
		services.AddSingleton<ImportedRecipeValidator>();
		services.AddSingleton(sp => new DomainFacade(
			sp.GetRequiredService<ConfigRegistry>(),
			sp.GetRequiredService<ICoreService>(),
			sp.GetRequiredService<RecipeStateManager>(),
			sp.GetRequiredService<RecipeHistoryManager>(),
			sp.GetRequiredService<ICsvService>(),
			sp.GetRequiredService<IS7Service>(),
			sp.GetRequiredService<IClipboardService>(),
			sp.GetRequiredService<ImportedRecipeValidator>(),
			sp.GetRequiredService<IPropertyParser>(),
			sp.GetRequiredService<IPlcSyncService>()));

		return services;
	}
}
