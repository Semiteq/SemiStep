using System.Globalization;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Analysis;
using SemiStep.Core.Recipes.Errors;
using SemiStep.Core.Recipes.Formulas;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Integration.Recipes;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "Mutation")]
public sealed class RecipeSessionValueRangeTests
{
	private const int BoundActionId = 11;
	private const string AmountColumnKey = "amount";
	private const string BoundPropertyTypeId = "int_bounded";
	private const int MaximumAmount = 100;

	[Fact]
	public void UpdateStepProperty_ValueAboveMaximum_FailsWithUndecoratedTypedError()
	{
		var session = BuildSession();
		session.AppendStep(BoundActionId).IsSuccess.Should().BeTrue();

		var result = session.UpdateStepProperty(0, AmountColumnKey, (MaximumAmount + 1).ToString(CultureInfo.InvariantCulture));

		var error = result.Errors.Should().ContainSingle().Which.Should().BeOfType<ValueAboveMaximumError>().Subject;
		error.Max.Should().Be(MaximumAmount);
		error.Id.Should().Be(BoundPropertyTypeId);
	}

	private static RecipeSession BuildSession()
	{
		var actions = new Dictionary<int, ActionDefinition>
		{
			[BoundActionId] = new ActionDefinition(
				id: BoundActionId,
				uiName: "Bound",
				deployDuration: DeployDuration.Immediate,
				properties:
				[
					new ActionPropertyDefinition(
						Key: AmountColumnKey,
						GroupName: null,
						PropertyTypeId: BoundPropertyTypeId,
						DefaultValue: null)
				])
		};
		var registry = TestRecipeMetadataRegistryFactory.Build(
			[
				TestPropertyTypeDefinitionBuilder.CreateInt(BoundPropertyTypeId, min: 0, max: MaximumAmount),
				TestPropertyTypeDefinitionBuilder.CreateString("text", maxLength: 8)
			],
			actions);

		return new RecipeSession(
			new RecipeAnalyzer(registry),
			registry,
			new FormulaEvaluator(registry, NullLogger<FormulaEvaluator>.Instance),
			new StubPlcSyncService(),
			NullLogger<RecipeSession>.Instance);
	}
}
