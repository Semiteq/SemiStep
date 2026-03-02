namespace Core.Exceptions;

public sealed class FormulaComputationOverflowException(string message) : Exception(message);
