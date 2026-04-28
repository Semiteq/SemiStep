using Core.Analysis;
using Core.Formulas;

using Domain.Facade;
using Domain.Helpers;
using Domain.Plc;
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

		services.AddSingleton(sp => new RecipeWorkspace(
			sp.GetRequiredService<RecipeStateManager>(),
			sp.GetRequiredService<RecipeHistoryManager>(),
			sp.GetRequiredService<RecipeAnalyzer>(),
			sp.GetRequiredService<IPlcSyncService>()));

		services.AddSingleton(sp => new RecipeEditor(
			sp.GetRequiredService<RecipeWorkspace>(),
			sp.GetRequiredService<ConfigRegistry>(),
			sp.GetRequiredService<FormulaApplicationCoordinator>(),
			sp.GetRequiredService<IPropertyParser>()));

		services.AddSingleton(sp => new PlcLifecycleManager(
			sp.GetRequiredService<RecipeWorkspace>(),
			sp.GetRequiredService<IS7Service>(),
			sp.GetRequiredService<RecipeAnalyzer>(),
			sp.GetRequiredService<RecipeHistoryManager>(),
			sp.GetRequiredService<RecipeStateManager>(),
			sp.GetRequiredService<IPlcSyncService>()));

		return services;
	}
}
