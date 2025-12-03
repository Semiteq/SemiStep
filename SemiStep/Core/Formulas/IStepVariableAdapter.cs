using Core.Entities;

using FluentResults;

namespace Core.Formulas;

public interface IStepVariableAdapter
{
	Result<IReadOnlyDictionary<string, double>> ExtractVariables(
		Step step,
		IReadOnlyList<string> variableNames);

	Result<Step> ApplyChanges(
		Step originalStep,
		IReadOnlyDictionary<string, double> variableUpdates);
}
