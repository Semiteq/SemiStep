using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Recipes;

namespace SemiStep.Tests.Helpers;

/// <summary>
/// Shared helpers for building <c>RecipeMetadataRegistry</c> instances in tests that do not exercise
/// string properties directly. The registry constructor validates the SoT invariant that at least one
/// <c>system_type=string</c> property is defined; tests that otherwise pass empty Properties must
/// supply at least one string-typed property to satisfy this invariant.
/// </summary>
internal static class TestRecipeMetadataRegistryFactory
{
	public const int DefaultStringMaxLength = 32;

	public static Dictionary<string, PropertyTypeDefinition> DefaultStringProperty()
	{
		return new Dictionary<string, PropertyTypeDefinition>(StringComparer.OrdinalIgnoreCase)
		{
			["comment"] = TestPropertyTypeDefinitionBuilder.CreateString("comment", DefaultStringMaxLength)
		};
	}

	/// <summary>
	/// Builds a <see cref="RecipeMetadataRegistry"/> from arbitrary property definitions plus
	/// optional actions/groups/columns. Used by tests that need a registry whose Properties shape
	/// differs from <see cref="DefaultStringProperty"/>; consolidates the boilerplate of wrapping
	/// the properties into an <see cref="AppConfiguration"/> and constructing the registry.
	/// </summary>
	public static RecipeMetadataRegistry Build(
		IEnumerable<PropertyTypeDefinition> properties,
		Dictionary<int, ActionDefinition>? actions = null,
		Dictionary<string, GroupDefinition>? groups = null,
		Dictionary<string, GridColumnDefinition>? columns = null)
	{
		var propertyMap = new Dictionary<string, PropertyTypeDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (var property in properties)
		{
			propertyMap[property.Id] = property;
		}

		var config = new AppConfiguration(
			Properties: propertyMap,
			Columns: columns ?? new Dictionary<string, GridColumnDefinition>(),
			Groups: groups ?? new Dictionary<string, GroupDefinition>(),
			Actions: actions ?? new Dictionary<int, ActionDefinition>(),
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default,
			Ui: AppUiOptions.Default);

		return new RecipeMetadataRegistry(config);
	}
}
