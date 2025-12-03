using FluentResults;

namespace Core.Formulas;

public interface IFormulaEngine
{
	Result<IReadOnlyDictionary<string, double>> Calculate(
		short actionId,
		string changedVariable,
		IReadOnlyDictionary<string, double> currentValues);
}
