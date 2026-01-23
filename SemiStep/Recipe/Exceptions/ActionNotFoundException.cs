namespace Recipe.Exceptions;

public sealed class ActionNotFoundException(string actionKey) : RecipeException($"Action '{actionKey}' not found");
