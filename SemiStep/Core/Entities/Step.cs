using System.Collections.Immutable;

using Core.Properties;
using Core.Reasons.Errors;

namespace Core.Entities;

/// <summary>
/// Represents a single, immutable step in a recipe. A step consists of a collection of properties.
/// </summary>
public sealed record Step
{
	/// <summary>
	/// A dictionary of all properties for this step, keyed by their column identifier.
	/// Properties not applicable to this step's action have a null value.
	/// </summary>
	public ImmutableDictionary<ColumnIdentifier, Property?> Properties { get; init; }

	/// <summary>
	/// The deployment duration type of the action associated with this step.
	/// </summary>
	public DeployDuration DeployDuration { get; init; }

	public Step(
		ImmutableDictionary<ColumnIdentifier, Property?> properties,
		DeployDuration deployDuration)
	{
		Properties = properties;
		DeployDuration = deployDuration;
	}

	public Result<Property> GetProperty(ColumnIdentifier columnId)
	{
		if (Properties.TryGetValue(columnId, out var property) && property != null)
			return property;

		return new CoreStepColumnNotFoundError();
	}
}
