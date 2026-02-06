namespace Shared.Entities;

public sealed record PlcRecipeData(
	int[] IntValues,
	float[] FloatValues,
	string[] StringValues,
	int StepCount)
{
	public static PlcRecipeData Empty => new([], [], [], 0);
}
