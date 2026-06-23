namespace SemiStep.Core.Recipes.Helpers;

/// <summary>
/// Computes the set of active column keys for a single step from the resolved action's
/// per-column activation conditions and the step's current selector values.
/// A column is active iff every <see cref="ActivationCondition"/> on it is met by the step's
/// value for that selector. A column with no activation conditions is always active.
/// Columns absent from the action's <see cref="ActionDefinition.Properties"/> are not part of
/// the action and never appear in the active set.
/// </summary>
public static class ActiveColumnSetResolver
{
	public static IReadOnlySet<string> Resolve(ActionDefinition action, Step step)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(step);

		var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var property in action.Properties)
		{
			if (IsColumnActive(property, step))
			{
				active.Add(property.Key);
			}
		}

		return active;
	}

	private static bool IsColumnActive(ActionPropertyDefinition property, Step step)
	{
		if (property.Activation is null || property.Activation.Count == 0)
		{
			return true;
		}

		foreach (var condition in property.Activation)
		{
			if (!IsConditionMet(condition, step))
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsConditionMet(ActivationCondition condition, Step step)
	{
		var selectorId = new PropertyId(condition.SelectorKey);
		if (!step.Properties.TryGetValue(selectorId, out var value))
		{
			return false;
		}

		return value.Type == PropertyType.Int && value.AsInt() == condition.EnablingValue;
	}
}
