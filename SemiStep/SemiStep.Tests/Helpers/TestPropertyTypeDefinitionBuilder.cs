using SemiStep.Core.Recipes;

namespace SemiStep.Tests.Helpers;

/// <summary>
/// Concise factories for <see cref="PropertyTypeDefinition"/> instances in tests, so individual
/// test sites do not repeat the full record-initialiser with null-padded optional fields.
/// </summary>
internal static class TestPropertyTypeDefinitionBuilder
{
	public static PropertyTypeDefinition CreateString(string id, int? maxLength)
	{
		return new PropertyTypeDefinition(
			Id: id,
			SystemType: "string",
			FormatKind: "numeric",
			Units: null,
			Min: null,
			Max: null,
			MaxLength: maxLength);
	}

	public static PropertyTypeDefinition CreateInt(string id, int? min = null, int? max = null)
	{
		return new PropertyTypeDefinition(
			Id: id,
			SystemType: "int",
			FormatKind: "numeric",
			Units: null,
			Min: min,
			Max: max,
			MaxLength: null);
	}

	public static PropertyTypeDefinition CreateFloat(string id, int? min = null, int? max = null)
	{
		return new PropertyTypeDefinition(
			Id: id,
			SystemType: "float",
			FormatKind: "numeric",
			Units: null,
			Min: min,
			Max: max,
			MaxLength: null);
	}
}
