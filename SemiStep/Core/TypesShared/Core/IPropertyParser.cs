using FluentResults;

namespace TypesShared.Core;

public interface IPropertyParser
{
	Result<PropertyValue> Parse(string input, PropertyTypeDefinition propertyDefinition);
}
