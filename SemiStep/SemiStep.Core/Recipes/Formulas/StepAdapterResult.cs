namespace SemiStep.Core.Recipes.Formulas;

internal sealed record StepAdapterResult(Step Step, IReadOnlyDictionary<string, double> Variables);
