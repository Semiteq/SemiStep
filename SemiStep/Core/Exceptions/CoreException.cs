namespace Core.Exceptions;

public abstract class CoreException : Exception
{
	protected CoreException(string message) : base(message)
	{
	}

	protected CoreException(string message, Exception inner) : base(message, inner)
	{
	}
}
