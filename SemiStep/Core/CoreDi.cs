using Core.Analysis;
using Core.Formulas;
using Core.Services;

using Microsoft.Extensions.DependencyInjection;

using TypesShared.Core;

namespace Core;

public static class CoreDi
{
	public static IServiceCollection AddRecipe(this IServiceCollection services)
	{
		services.AddSingleton<CoreConfig>();

		services.AddSingleton<RecipeAnalyzer>();
		services.AddSingleton<LoopParser>();

		services.AddSingleton<IReadOnlyDictionary<int, CompiledFormula>>(_ => new Dictionary<int, CompiledFormula>());
		services.AddSingleton<FormulaEngine>();
		services.AddSingleton<FormulaApplicationCoordinator>();

		services.AddSingleton<IPropertyParser, PropertyParser>();

		return services;
	}
}
