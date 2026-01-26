using Shared.Entities;

namespace Shared.Registries;

public interface IPropertyRegistry
{
	void Initialize(IReadOnlyDictionary<string, PropertyDefinition> properties);
	PropertyDefinition GetProperty(string propertyTypeId);
	bool PropertyExists(string propertyTypeId);
	IReadOnlyList<PropertyDefinition> GetAll();
}
