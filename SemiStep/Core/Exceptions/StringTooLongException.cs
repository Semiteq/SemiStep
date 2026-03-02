namespace Core.Exceptions;

public sealed class StringTooLongException(string message) : Exception(message);
