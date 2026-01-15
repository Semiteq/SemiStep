using Core.Properties.Contracts;

using FluentResults;

namespace Core.Properties.Definitions;

internal class EnumDefinition : IPropertyDefinition
{
	public string Units { get; }
	public Type SystemType { get; }
	public Result<object> GetValidatedValue(object value)
	{
		throw new NotImplementedException();
	}

	public Result<object> TryParse(string input)
	{
		throw new NotImplementedException();
	}

	public string FormatValue(object value)
	{
		throw new NotImplementedException();
	}

	public object DefaultValue { get; }
	public Result<object> GetValueSignRespected(object value)
	{
		throw new NotImplementedException();
	}
}
