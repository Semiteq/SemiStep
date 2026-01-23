using Shared.Entities;

namespace Shared.Registries;

public interface IPropertyRegistry
{
	PropertyDefinition GetProperty(string propertyTypeId);

	bool PropertyExists(string propertyTypeId);

	IReadOnlyList<PropertyDefinition> GetAll();
}
