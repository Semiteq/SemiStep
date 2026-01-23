namespace Recipe.Exceptions;

public abstract class RecipeException : Exception
{
	protected RecipeException(string message) : base(message)
	{
	}

	protected RecipeException(string message, Exception inner) : base(message, inner)
	{
	}
}
