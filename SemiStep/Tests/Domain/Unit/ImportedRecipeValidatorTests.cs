using System.Collections.Immutable;

using FluentAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;

using Xunit;

namespace Tests.Domain.Unit;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
[Trait("Area", "ImportValidation")]
public sealed class ImportedRecipeValidatorTests
{
	private const int ValveActionId = 50;
	private const string ValveGroupId = "valve";
	private const string TargetColumnKey = "target";
	private const int ValidGroupKey = 1;
	private const int InvalidGroupKey = 99;

	private static RecipeMetadataRegistry BuildRecipeMetadataRegistry(
		Dictionary<int, ActionDefinition>? actions = null,
		Dictionary<string, GroupDefinition>? groups = null)
	{
		var config = new AppConfiguration(
			Properties: new Dictionary<string, PropertyTypeDefinition>(),
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: groups ?? new Dictionary<string, GroupDefinition>(),
			Actions: actions ?? new Dictionary<int, ActionDefinition>(),
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default);

		return new RecipeMetadataRegistry(config);
	}

	private static ImportedRecipeValidator BuildValidator()
	{
		var actions = new Dictionary<int, ActionDefinition>
		{
			[ValveActionId] = new ActionDefinition(
				Id: ValveActionId,
				UiName: "Valve",
				DeployDuration: "immediate",
				Properties: new[]
				{
					new ActionPropertyDefinition(
						Key: TargetColumnKey,
						GroupName: ValveGroupId,
						PropertyTypeId: "enum",
						DefaultValue: null)
				})
		};

		var groups = new Dictionary<string, GroupDefinition>
		{
			[ValveGroupId] = new GroupDefinition(
				GroupId: ValveGroupId,
				Items: new Dictionary<int, string>
				{
					[1] = "Open",
					[2] = "Close"
				})
		};

		var registry = BuildRecipeMetadataRegistry(actions, groups);
		return new ImportedRecipeValidator(registry);
	}

	private static Recipe BuildRecipeWithStep(int actionId, string columnKey, PropertyValue value)
	{
		var step = new Step(
			actionId,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId(columnKey), value));

		return new Recipe(ImmutableList.Create(step));
	}

	[Fact]
	public void Validate_ValidGroupKey_ReturnsSuccess()
	{
		var validator = BuildValidator();
		var recipe = BuildRecipeWithStep(ValveActionId, TargetColumnKey, PropertyValue.FromInt(ValidGroupKey));

		var result = validator.Validate(recipe);

		result.IsSuccess.Should().BeTrue();
	}

	[Fact]
	public void Validate_InvalidGroupKey_ReturnsFail()
	{
		var validator = BuildValidator();
		var recipe = BuildRecipeWithStep(ValveActionId, TargetColumnKey, PropertyValue.FromInt(InvalidGroupKey));

		var result = validator.Validate(recipe);

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public void Validate_InvalidGroupKey_ErrorMessageContainsStepNumberAndGroupName()
	{
		var validator = BuildValidator();
		var recipe = BuildRecipeWithStep(ValveActionId, TargetColumnKey, PropertyValue.FromInt(InvalidGroupKey));

		var result = validator.Validate(recipe);

		result.Errors.Should().ContainSingle()
			.Which.Message.Should().Contain("Step 1")
			.And.Contain(ValveGroupId);
	}

	[Fact]
	public void Validate_NonGroupColumn_IsNotChecked()
	{
		var actions = new Dictionary<int, ActionDefinition>
		{
			[10] = new ActionDefinition(
				Id: 10,
				UiName: "Wait",
				DeployDuration: "longlasting",
				Properties: new[]
				{
					new ActionPropertyDefinition(
						Key: "step_duration",
						GroupName: null,
						PropertyTypeId: "time",
						DefaultValue: "10")
				})
		};

		var registry = BuildRecipeMetadataRegistry(actions);
		var validator = new ImportedRecipeValidator(registry);
		var step = new Step(
			10,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId("step_duration"), PropertyValue.FromFloat(5f)));
		var recipe = new Recipe(ImmutableList.Create(step));

		var result = validator.Validate(recipe);

		result.IsSuccess.Should().BeTrue("non-group columns are not subject to group membership checks");
	}

	[Fact]
	public void Validate_EmptyRecipe_ReturnsSuccess()
	{
		var validator = BuildValidator();
		var recipe = new Recipe(ImmutableList<Step>.Empty);

		var result = validator.Validate(recipe);

		result.IsSuccess.Should().BeTrue();
	}

	[Fact]
	public void Validate_MultipleInvalidSteps_ReportsAllErrors()
	{
		var validator = BuildValidator();
		var step1 = new Step(ValveActionId,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId(TargetColumnKey), PropertyValue.FromInt(InvalidGroupKey)));
		var step2 = new Step(ValveActionId,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId(TargetColumnKey), PropertyValue.FromInt(InvalidGroupKey)));
		var recipe = new Recipe(ImmutableList.Create(step1, step2));

		var result = validator.Validate(recipe);

		result.Errors.Should().HaveCount(2);
	}
}
