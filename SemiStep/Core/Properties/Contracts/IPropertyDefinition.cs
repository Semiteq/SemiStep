using FluentResults;

namespace Core.Properties.Contracts;

internal interface IPropertyDefinition
{
	string Units { get; }
	Type SystemType { get; }
	object DefaultValue { get; }

	string FormatValue(object value);
	Result<object> GetValidatedValue(object value);
	Result<object> GetValueSignRespected(object value);
}
