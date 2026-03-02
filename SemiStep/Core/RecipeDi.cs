using Core.Analysis;
using Core.Entities;
using Core.Formulas;
using Core.Services;

using Microsoft.Extensions.DependencyInjection;

using Serilog;

namespace Core;

public static class RecipeDi
{
	public static IServiceCollection AddRecipe(this IServiceCollection services)
	{
		services.AddSingleton<CoreConfig>();

		// Core services
		services.AddSingleton<StepFactory>();
		services.AddSingleton<RecipeMutator>();
		services.AddSingleton<PropertyValidator>();

		// Analysis
		services.AddSingleton<TimingCalculator>();
		services.AddSingleton<RecipeAnalyzer>();
		services.AddSingleton<LoopParser>();

		// Formulas (placeholder - empty formulas dictionary for now)
		services.AddSingleton<IReadOnlyDictionary<int, CompiledFormula>>(_ => new Dictionary<int, CompiledFormula>());
		services.AddSingleton<IFormulaEngine, FormulaEngine>();
		services.AddSingleton<IStepVariableAdapter, StepVariableAdapter>();
		services.AddSingleton<FormulaApplicationCoordinator>();

		// Facade
		services.AddSingleton<CoreFacade>();

		return services;
	}
}
