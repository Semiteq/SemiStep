namespace Core.Reasons.Errors;

public sealed class CoreFormulaDivisionByZeroError : BilingualError
{
	public string VariableName { get; }

	public CoreFormulaDivisionByZeroError(string variableName)
		: base(
			$"Division by zero while computing variable '{variableName}'",
			$"Деление на ноль при вычислении переменной '{variableName}'")
	{
		VariableName = variableName;
	}
}
