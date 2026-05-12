using FluentResults;

namespace SemiStep.Core.Recipes.Formulas;

public sealed class FormulaApplicationCoordinator(FormulaEngine engine)
{
	public Result<Step> ApplyIfExists(
		Step step,
		ActionDefinition action,
		PropertyId changedProperty,
		FormulaDefinition? formulaDefinition)
	{
		if (formulaDefinition is null)
		{
			return step;
		}

		if (IsFormulaNotNeeded(changedProperty, formulaDefinition))
		{
			return step;
		}

		var extractResult = StepVariableAdapter.ExtractVariables(step, formulaDefinition.RecalcOrder);
		if (extractResult.IsFailed)
		{
			return extractResult.ToResult<Step>();
		}

		var calcResult = engine.Calculate(action.Id, changedProperty.Value, extractResult.Value);
		if (calcResult.IsFailed)
		{
			return calcResult.ToResult<Step>();
		}

		return StepVariableAdapter.ApplyChanges(step, calcResult.Value);
	}

	private static bool IsFormulaNotNeeded(PropertyId changedProperty, FormulaDefinition formula)
	{
		return !formula.RecalcOrder.Contains(changedProperty.Value, StringComparer.OrdinalIgnoreCase);
	}
}
