using Core.Properties.Contracts;

using FluentResults;

namespace Core.Definitions.Contracts;

internal interface IPropertySchema
{
	Result<IPropertyDefinition> GetDefinition(ActionDefinition action, string columnId);
}
