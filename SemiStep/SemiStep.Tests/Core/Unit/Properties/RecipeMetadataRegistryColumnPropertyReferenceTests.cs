using FluentAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Properties;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Area", "RecipeMetadataRegistry")]
public sealed class RecipeMetadataRegistryColumnPropertyReferenceTests
{
	[Fact]
	public void Constructor_ColumnReferencesUnknownProperty_Throws()
	{
		var columns = new Dictionary<string, GridColumnDefinition>(StringComparer.OrdinalIgnoreCase)
		{
			["dangling"] = new GridColumnDefinition(
				Key: "dangling",
				ColumnType: "text",
				UiName: "Dangling",
				PropertyTypeId: "ghost",
				ReadOnly: false,
				SaveToCsv: false)
		};

		var action = () => TestRecipeMetadataRegistryFactory.Build(
			new[] { TestPropertyTypeDefinitionBuilder.CreateString("comment", maxLength: 32) },
			columns: columns);

		action.Should().Throw<InvalidOperationException>()
			.WithMessage("*dangling*")
			.WithMessage("*ghost*");
	}

	[Fact]
	public void Constructor_ColumnWithEmptyPropertyTypeId_IsAllowed()
	{
		var columns = new Dictionary<string, GridColumnDefinition>(StringComparer.OrdinalIgnoreCase)
		{
			["plain"] = new GridColumnDefinition(
				Key: "plain",
				ColumnType: "text",
				UiName: "Plain",
				PropertyTypeId: string.Empty,
				ReadOnly: true,
				SaveToCsv: false)
		};

		var action = () => TestRecipeMetadataRegistryFactory.Build(
			new[] { TestPropertyTypeDefinitionBuilder.CreateString("comment", maxLength: 32) },
			columns: columns);

		action.Should().NotThrow();
	}

	[Fact]
	public void Constructor_ColumnReferencesExistingProperty_Succeeds()
	{
		var columns = new Dictionary<string, GridColumnDefinition>(StringComparer.OrdinalIgnoreCase)
		{
			["comment"] = new GridColumnDefinition(
				Key: "comment",
				ColumnType: "text",
				UiName: "Comment",
				PropertyTypeId: "comment",
				ReadOnly: false,
				SaveToCsv: true)
		};

		var action = () => TestRecipeMetadataRegistryFactory.Build(
			new[] { TestPropertyTypeDefinitionBuilder.CreateString("comment", maxLength: 32) },
			columns: columns);

		action.Should().NotThrow();
	}
}
