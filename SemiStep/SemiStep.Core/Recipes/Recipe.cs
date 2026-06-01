using System.Collections.Immutable;

namespace SemiStep.Core.Recipes;

public sealed record Recipe(ImmutableList<Step> Steps)
{
	public static readonly Recipe Empty = new([]);

	public int StepCount => Steps.Count;

	public Recipe AppendStep(Step step)
	{
		return this with { Steps = Steps.Add(step) };
	}

	public Recipe InsertStep(int index, Step step)
	{
		return this with { Steps = Steps.Insert(index, step) };
	}

	public Recipe RemoveStep(int index)
	{
		return this with { Steps = Steps.RemoveAt(index) };
	}

	public Recipe InsertSteps(int startIndex, IReadOnlyList<Step> steps)
	{
		return this with { Steps = Steps.InsertRange(startIndex, steps) };
	}

	public Recipe RemoveSteps(IReadOnlyList<int> indices)
	{
		var sortedDescending = indices.OrderByDescending(i => i).ToList();
		var newSteps = Steps;
		foreach (var index in sortedDescending)
		{
			newSteps = newSteps.RemoveAt(index);
		}

		return this with { Steps = newSteps };
	}

	public Recipe ReplaceStep(int index, Step step)
	{
		return this with { Steps = Steps.SetItem(index, step) };
	}

	public bool Equals(Recipe? other)
	{
		if (other is null)
		{
			return false;
		}

		if (ReferenceEquals(this, other))
		{
			return true;
		}

		return Steps.SequenceEqual(other.Steps);
	}

	public override int GetHashCode()
	{
		var hash = new HashCode();
		foreach (var step in Steps)
		{
			hash.Add(step);
		}

		return hash.ToHashCode();
	}
}
