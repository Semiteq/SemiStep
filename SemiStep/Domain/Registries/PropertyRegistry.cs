using Shared.Entities;
using Shared.Registries;

namespace Domain.Registries;

public sealed class PropertyRegistry : IPropertyRegistry
{
	private readonly Dictionary<string, PropertyDefinition> _properties = new(StringComparer.OrdinalIgnoreCase);

	public void Initialize(IReadOnlyDictionary<string, PropertyDefinition> properties)
	{
		_properties.Clear();

		foreach (var (key, property) in properties)
		{
			_properties[key] = property;
		}
	}

	public PropertyDefinition GetProperty(string propertyTypeId)
	{
		if (!_properties.TryGetValue(propertyTypeId, out var property))
		{
			throw new KeyNotFoundException($"Property with id '{propertyTypeId}' not found");
		}

		return property;
	}

	public bool PropertyExists(string propertyTypeId) => _properties.ContainsKey(propertyTypeId);

	public IReadOnlyList<PropertyDefinition> GetAll() => _properties.Values.ToList();
}
