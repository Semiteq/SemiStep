using System.Collections.Immutable;

using FluentAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Domain.Unit;

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
		Dictionary<string, GroupDefinition>? groups = null,
		IEnumerable<PropertyTypeDefinition>? properties = null)
	{
		return TestRecipeMetadataRegistryFactory.Build(
			properties ?? DefaultProperties(),
			actions,
			groups);
	}

	private static IEnumerable<PropertyTypeDefinition> DefaultProperties()
	{
		return new[]
		{
			TestPropertyTypeDefinitionBuilder.CreateInt("enum"),
			TestPropertyTypeDefinitionBuilder.CreateFloat("time"),
			TestPropertyTypeDefinitionBuilder.CreateString("text", maxLength: 8),
			TestPropertyTypeDefinitionBuilder.CreateInt("int_bounded", min: 0, max: 100)
		};
	}

	private static ImportedRecipeValidator BuildValidator()
	{
		var actions = new Dictionary<int, ActionDefinition>
		{
			[ValveActionId] = new ActionDefinition(
				id: ValveActionId,
				uiName: "Valve",
				deployDuration: DeployDuration.Immediate,
				properties: new[]
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
	public void Validate_NonGroupColumnWithoutConstraints_PassesValidation()
	{
		var actions = new Dictionary<int, ActionDefinition>
		{
			[10] = new ActionDefinition(
				id: 10,
				uiName: "Wait",
				deployDuration: DeployDuration.LongLasting,
				properties: new[]
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

		result.IsSuccess.Should().BeTrue(
			"non-group columns without Min/Max/MaxLength constraints have no PropertyValidator rules to violate");
	}

	[Fact]
	public void Validate_NonGroupColumnWithViolatingValue_IsRejected()
	{
		var actions = new Dictionary<int, ActionDefinition>
		{
			[11] = new ActionDefinition(
				id: 11,
				uiName: "Bound",
				deployDuration: DeployDuration.Immediate,
				properties: new[]
				{
					new ActionPropertyDefinition(
						Key: "amount",
						GroupName: null,
						PropertyTypeId: "int_bounded",
						DefaultValue: null)
				})
		};

		var registry = BuildRecipeMetadataRegistry(actions);
		var validator = new ImportedRecipeValidator(registry);
		var step = new Step(
			11,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId("amount"), PropertyValue.FromInt(500)));
		var recipe = new Recipe(ImmutableList.Create(step));

		var result = validator.Validate(recipe);

		result.IsFailed.Should().BeTrue(
			"non-group columns ARE validated against their property's Min/Max/MaxLength via PropertyValidator");
		result.Errors.Should().Contain(error =>
			error.Message.Contains("Step 1")
			&& error.Message.Contains("amount")
			&& error.Message.Contains("exceeds maximum"));
	}

	[Fact]
	public void Validate_NonGroupColumnWithTypeMismatch_IsRejected()
	{
		var actions = new Dictionary<int, ActionDefinition>
		{
			[12] = new ActionDefinition(
				id: 12,
				uiName: "Bound",
				deployDuration: DeployDuration.Immediate,
				properties: new[]
				{
					new ActionPropertyDefinition(
						Key: "amount",
						GroupName: null,
						PropertyTypeId: "int_bounded",
						DefaultValue: null)
				})
		};

		var registry = BuildRecipeMetadataRegistry(actions);
		var validator = new ImportedRecipeValidator(registry);
		var step = new Step(
			12,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId("amount"), PropertyValue.FromString("not-an-int")));
		var recipe = new Recipe(ImmutableList.Create(step));

		var result = validator.Validate(recipe);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(error =>
			error.Message.Contains("Step 1")
			&& error.Message.Contains("amount")
			&& error.Message.Contains("Expected int"));
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

	private const int CommentActionId = 200;
	private const string CommentColumnKey = "comment";
	private const string IntColumnKey = "amount";

	private static ImportedRecipeValidator BuildPropertyAwareValidator()
	{
		var actions = new Dictionary<int, ActionDefinition>
		{
			[CommentActionId] = new ActionDefinition(
				id: CommentActionId,
				uiName: "Annotate",
				deployDuration: DeployDuration.Immediate,
				properties: new[]
				{
					new ActionPropertyDefinition(
						Key: CommentColumnKey,
						GroupName: null,
						PropertyTypeId: "text",
						DefaultValue: null),
					new ActionPropertyDefinition(
						Key: IntColumnKey,
						GroupName: null,
						PropertyTypeId: "int_bounded",
						DefaultValue: null)
				})
		};

		var registry = BuildRecipeMetadataRegistry(actions);
		return new ImportedRecipeValidator(registry);
	}

	[Fact]
	public void Validate_StepWithOverLengthString_IsRejectedWithMaxLengthError()
	{
		var validator = BuildPropertyAwareValidator();
		var step = new Step(
			CommentActionId,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId(CommentColumnKey), PropertyValue.FromString("XXXXXXXXX")));
		var recipe = new Recipe(ImmutableList.Create(step));

		var result = validator.Validate(recipe);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(error =>
			error.Message.Contains("Step 1")
			&& error.Message.Contains(CommentColumnKey)
			&& error.Message.Contains("exceeds maximum")
			&& error.Message.Contains("9")
			&& error.Message.Contains("8"));
	}

	[Fact]
	public void Validate_StepWithOutOfRangeInt_IsRejected()
	{
		var validator = BuildPropertyAwareValidator();
		var step = new Step(
			CommentActionId,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId(IntColumnKey), PropertyValue.FromInt(500)));
		var recipe = new Recipe(ImmutableList.Create(step));

		var result = validator.Validate(recipe);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(error =>
			error.Message.Contains("Step 1")
			&& error.Message.Contains(IntColumnKey)
			&& error.Message.Contains("exceeds maximum")
			&& error.Message.Contains("100"));
	}

	[Fact]
	public void Validate_StepWithValidPropertyValues_PassesThrough()
	{
		var validator = BuildPropertyAwareValidator();
		var step = new Step(
			CommentActionId,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId(CommentColumnKey), PropertyValue.FromString("ok"))
				.Add(new PropertyId(IntColumnKey), PropertyValue.FromInt(42)));
		var recipe = new Recipe(ImmutableList.Create(step));

		var result = validator.Validate(recipe);

		result.IsSuccess.Should().BeTrue();
	}

	[Fact]
	public void Validate_MultiplePropertyViolationsAcrossSteps_AllReported()
	{
		var validator = BuildPropertyAwareValidator();
		var step1 = new Step(
			CommentActionId,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId(CommentColumnKey), PropertyValue.FromString("XXXXXXXXX")));
		var step2 = new Step(
			CommentActionId,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty
				.Add(new PropertyId(IntColumnKey), PropertyValue.FromInt(500)));
		var recipe = new Recipe(ImmutableList.Create(step1, step2));

		var result = validator.Validate(recipe);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().HaveCount(2);
		result.Errors.Should().Contain(error => error.Message.Contains("Step 1"));
		result.Errors.Should().Contain(error => error.Message.Contains("Step 2"));
	}
}
