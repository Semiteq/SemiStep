namespace Recipe.Exceptions;

public sealed class InvalidStepIndexException(int index, int maxIndex)
	: RecipeException($"Step index {index} is out of range [0, {maxIndex}]");
