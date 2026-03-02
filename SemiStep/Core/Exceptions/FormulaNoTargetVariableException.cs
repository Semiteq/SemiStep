namespace Core.Exceptions;

public sealed class FormulaNoTargetVariableException(string message) : Exception(message);
