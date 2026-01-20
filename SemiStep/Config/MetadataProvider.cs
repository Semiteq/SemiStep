using Core.Entities;
using Core.Exceptions;
using Core.Metadata;

namespace SemiStep.Config;

public sealed class MetadataProvider : IMetadataProvider
{
	private readonly Dictionary<string, ActionMetadata> _actions = new();
	private readonly Dictionary<ColumnId, PropertyMetadata> _properties = new();

	public void RegisterAction(ActionMetadata action)
	{
		_actions[action.Key] = action;
	}

	public void RegisterProperty(PropertyMetadata property)
	{
		_properties[property.ColumnId] = property;
	}

	public ActionMetadata GetAction(string actionKey)
	{
		if (!_actions.TryGetValue(actionKey, out var action))
			throw new ActionNotFoundException(actionKey);
		return action;
	}

	public PropertyMetadata GetProperty(ColumnId columnId)
	{
		if (!_properties.TryGetValue(columnId, out var property))
			throw new KeyNotFoundException($"Property not found: {columnId}");
		return property;
	}

	public IReadOnlyList<ActionMetadata> GetAllActions() => _actions.Values.ToList();

	public bool ActionExists(string actionKey) => _actions.ContainsKey(actionKey);

	public bool PropertyExists(ColumnId columnId) => _properties.ContainsKey(columnId);
}
