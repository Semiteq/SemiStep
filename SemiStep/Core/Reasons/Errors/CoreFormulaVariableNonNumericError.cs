namespace Core.Reasons.Errors;

public sealed class CoreFormulaVariableNonNumericError : BilingualError
{
	public string VariableName { get; }

	public CoreFormulaVariableNonNumericError(string variableName)
		: base(
			$"Formula variable '{variableName}' has a non-numeric type",
			$"Переменная формулы '{variableName}' имеет нечисловой тип")
	{
		VariableName = variableName;
	}
}
