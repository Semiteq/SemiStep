using Core.Entities;

using FluentResults;

namespace Core.Formulas;

public sealed class FormulaApplicationCoordinator
{
	private readonly IFormulaEngine _engine;
	private readonly IStepVariableAdapter _adapter;

	public FormulaApplicationCoordinator(IFormulaEngine engine, IStepVariableAdapter adapter)
	{
		_engine = engine ?? throw new ArgumentNullException(nameof(engine));
		_adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
	}

	public Result<Step> ApplyIfExists(Step step, ActionDefinition action, ColumnIdentifier changedColumn)
	{
		if (action.Formula == null)
			return Result.Ok(step);

		var formulaDefinition = action.Formula;

		if (!IsFormulaNeeded(changedColumn, formulaDefinition))
			return Result.Ok(step);

		var variablesResult = _adapter.ExtractVariables(step, formulaDefinition.RecalcOrder);
		if (variablesResult.IsFailed)
			return variablesResult.ToResult<Step>();

		var calculationResult = _engine.Calculate(action.Id, changedColumn.Value, variablesResult.Value);
		if (calculationResult.IsFailed)
			return calculationResult.ToResult<Step>();

		return _adapter.ApplyChanges(step, calculationResult.Value);
	}

	private static bool IsFormulaNeeded(ColumnIdentifier changedColumn, FormulaDefinition formula)
	{
		return formula.RecalcOrder.Contains(changedColumn.Value, StringComparer.OrdinalIgnoreCase);
	}
}
