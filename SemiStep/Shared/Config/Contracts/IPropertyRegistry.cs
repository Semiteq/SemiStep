using Shared.Core;

namespace Shared.Config.Contracts;

public interface IPropertyRegistry
{
	void Initialize(IReadOnlyDictionary<string, PropertyDefinition> properties);
	PropertyDefinition GetProperty(string propertyTypeId);
	bool PropertyExists(string propertyTypeId);
}
