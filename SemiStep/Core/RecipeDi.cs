using Core.Analysis;
using Core.Entities;
using Core.Formulas;
using Core.Services;

using Microsoft.Extensions.DependencyInjection;

using Serilog;

namespace Core;

public static class RecipeDi
{
	private const string DefaultIterationColumnName = "task";

	public static IServiceCollection AddRecipe(
		this IServiceCollection services,
		ILogger? logger = null,
		string? iterationColumnName = null)
	{
		if (logger is not null)
		{
			services.AddSingleton(logger);
		}

		// Iteration column configuration for loop parsing
		var columnName = iterationColumnName ?? DefaultIterationColumnName;
		var iterationColumn = new ColumnId(columnName);
		services.AddSingleton(typeof(ColumnId), iterationColumn);

		// Core services
		services.AddSingleton<StepFactory>();
		services.AddSingleton<RecipeMutator>();
		services.AddSingleton<PropertyValidator>();

		// Analysis
		services.AddSingleton<TimingCalculator>();
		services.AddSingleton<RecipeAnalyzer>();

		// Formulas (placeholder - empty formulas dictionary for now)
		services.AddSingleton<IReadOnlyDictionary<int, CompiledFormula>>(
			_ => new Dictionary<int, CompiledFormula>());
		services.AddSingleton<IFormulaEngine, FormulaEngine>();
		services.AddSingleton<IStepVariableAdapter, StepVariableAdapter>();
		services.AddSingleton<FormulaApplicationCoordinator>();

		// Facade
		services.AddSingleton<CoreFacade>();

		return services;
	}
}
