using System.Collections.Immutable;

using FluentAssertions;

using SemiStep.Core.Recipes;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Recipes;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Area", "RecipeEquality")]
public sealed class RecipeStructuralEqualityTests
{
	private static Step BuildStep(int actionKey, params (string Key, PropertyValue Value)[] properties)
	{
		var builder = ImmutableDictionary.CreateBuilder<PropertyId, PropertyValue>();
		foreach (var property in properties)
		{
			builder[new PropertyId(property.Key)] = property.Value;
		}

		return new Step(actionKey, builder.ToImmutable());
	}

	[Fact]
	public void StepEquals_IdenticalContentInDistinctDictionaries_AreEqual()
	{
		var first = BuildStep(1, ("temperature", PropertyValue.FromInt(200)));
		var second = BuildStep(1, ("temperature", PropertyValue.FromInt(200)));

		first.Should().NotBeSameAs(second);
		first.Properties.Should().NotBeSameAs(second.Properties);
		first.Equals(second).Should().BeTrue();
	}

	[Fact]
	public void StepEquals_DifferentActionKey_AreNotEqual()
	{
		var first = BuildStep(1, ("temperature", PropertyValue.FromInt(200)));
		var second = BuildStep(2, ("temperature", PropertyValue.FromInt(200)));

		first.Equals(second).Should().BeFalse();
	}

	[Fact]
	public void StepEquals_DifferentPropertyValue_AreNotEqual()
	{
		var first = BuildStep(1, ("temperature", PropertyValue.FromInt(200)));
		var second = BuildStep(1, ("temperature", PropertyValue.FromInt(201)));

		first.Equals(second).Should().BeFalse();
	}

	[Fact]
	public void StepEquals_IdenticalFloatAndStringContent_AreEqual()
	{
		var first = BuildStep(
			1,
			("rampRate", PropertyValue.FromFloat(1.5f)),
			("label", PropertyValue.FromString("anneal")));
		var second = BuildStep(
			1,
			("rampRate", PropertyValue.FromFloat(1.5f)),
			("label", PropertyValue.FromString("anneal")));

		first.Should().NotBeSameAs(second);
		first.Equals(second).Should().BeTrue();
	}

	[Fact]
	public void StepEquals_DifferentFloatValue_AreNotEqual()
	{
		var first = BuildStep(1, ("rampRate", PropertyValue.FromFloat(1.5f)));
		var second = BuildStep(1, ("rampRate", PropertyValue.FromFloat(1.6f)));

		first.Equals(second).Should().BeFalse();
	}

	[Fact]
	public void StepEquals_DifferentStringValue_AreNotEqual()
	{
		var first = BuildStep(1, ("label", PropertyValue.FromString("anneal")));
		var second = BuildStep(1, ("label", PropertyValue.FromString("cooldown")));

		first.Equals(second).Should().BeFalse();
	}

	[Fact]
	public void StepEquals_DifferentPropertyKeySet_AreNotEqual()
	{
		var first = BuildStep(1, ("temperature", PropertyValue.FromInt(200)));
		var second = BuildStep(1, ("pressure", PropertyValue.FromInt(200)));

		first.Equals(second).Should().BeFalse();
	}

	[Fact]
	public void StepEquals_DifferentPropertyCount_AreNotEqual()
	{
		var first = BuildStep(
			1,
			("temperature", PropertyValue.FromInt(200)),
			("pressure", PropertyValue.FromInt(50)));
		var second = BuildStep(1, ("temperature", PropertyValue.FromInt(200)));

		first.Equals(second).Should().BeFalse();
	}

	[Fact]
	public void StepEquals_SameContentDifferentInsertionOrder_AreEqual()
	{
		var first = BuildStep(
			1,
			("temperature", PropertyValue.FromInt(200)),
			("pressure", PropertyValue.FromInt(50)));
		var second = BuildStep(
			1,
			("pressure", PropertyValue.FromInt(50)),
			("temperature", PropertyValue.FromInt(200)));

		first.Equals(second).Should().BeTrue();
	}

	[Fact]
	public void StepGetHashCode_EqualSteps_ProduceEqualHashCodes()
	{
		var first = BuildStep(
			1,
			("temperature", PropertyValue.FromInt(200)),
			("pressure", PropertyValue.FromInt(50)));
		var second = BuildStep(
			1,
			("pressure", PropertyValue.FromInt(50)),
			("temperature", PropertyValue.FromInt(200)));

		first.GetHashCode().Should().Be(second.GetHashCode());
	}

	[Fact]
	public void StepEquals_Null_IsFalse()
	{
		var step = BuildStep(1, ("temperature", PropertyValue.FromInt(200)));

		step.Equals(null).Should().BeFalse();
	}

	[Fact]
	public void StepEquals_Self_IsTrue()
	{
		var step = BuildStep(1, ("temperature", PropertyValue.FromInt(200)));

		step.Equals(step).Should().BeTrue();
	}

	[Fact]
	public void RecipeEquals_IdenticalStepsInDistinctLists_AreEqual()
	{
		var step = BuildStep(1, ("temperature", PropertyValue.FromInt(200)));
		var first = new Recipe([step]);
		var second = new Recipe([step]);

		first.Should().NotBeSameAs(second);
		first.Steps.Should().NotBeSameAs(second.Steps);
		first.Equals(second).Should().BeTrue();
	}

	[Fact]
	public void RecipeEquals_EmptyEqualsNewEmptyRecipe()
	{
		Recipe.Empty.Equals(new Recipe([])).Should().BeTrue();
	}

	[Fact]
	public void RecipeEquals_DifferentStepOrder_AreNotEqual()
	{
		var firstStep = BuildStep(1, ("temperature", PropertyValue.FromInt(200)));
		var secondStep = BuildStep(2, ("pressure", PropertyValue.FromInt(50)));
		var first = new Recipe([firstStep, secondStep]);
		var second = new Recipe([secondStep, firstStep]);

		first.Equals(second).Should().BeFalse();
	}

	[Fact]
	public void RecipeEquals_DifferentStepCount_AreNotEqual()
	{
		var firstStep = BuildStep(1, ("temperature", PropertyValue.FromInt(200)));
		var secondStep = BuildStep(2, ("pressure", PropertyValue.FromInt(50)));
		var first = new Recipe([firstStep, secondStep]);
		var second = new Recipe([firstStep]);

		first.Equals(second).Should().BeFalse();
	}

	[Fact]
	public void RecipeEquals_ContentEqualStepWithFreshInstances_AreEqual()
	{
		var local = new Recipe([BuildStep(1, ("temperature", PropertyValue.FromInt(200)))]);
		var plc = DeepCopy(local);

		plc.Should().NotBeSameAs(local);
		plc.Steps.Should().NotBeSameAs(local.Steps);
		plc.Steps[0].Should().NotBeSameAs(local.Steps[0]);
		plc.Steps[0].Properties.Should().NotBeSameAs(local.Steps[0].Properties);
		local.Equals(plc).Should().BeTrue();
	}

	[Fact]
	public void RecipeEquals_FloatAndStringStepWithFreshInstances_AreEqual()
	{
		var local = new Recipe([
			BuildStep(
				1,
				("rampRate", PropertyValue.FromFloat(1.5f)),
				("label", PropertyValue.FromString("anneal")))
		]);
		var plc = DeepCopy(local);

		plc.Should().NotBeSameAs(local);
		plc.Steps[0].Properties.Should().NotBeSameAs(local.Steps[0].Properties);
		local.Equals(plc).Should().BeTrue();
	}

	[Fact]
	public void RecipeGetHashCode_EqualRecipes_ProduceEqualHashCodes()
	{
		var step = BuildStep(1, ("temperature", PropertyValue.FromInt(200)));
		var first = new Recipe([step]);
		var second = DeepCopy(first);

		first.GetHashCode().Should().Be(second.GetHashCode());
	}

	[Fact]
	public void RecipeEquals_Null_IsFalse()
	{
		var recipe = new Recipe([BuildStep(1, ("temperature", PropertyValue.FromInt(200)))]);

		recipe.Equals(null).Should().BeFalse();
	}

	[Fact]
	public void RecipeEquals_Self_IsTrue()
	{
		var recipe = new Recipe([BuildStep(1, ("temperature", PropertyValue.FromInt(200)))]);

		recipe.Equals(recipe).Should().BeTrue();
	}

	[Fact]
	public void PropertyValueEquals_SameNumberDifferentType_AreNotEqual()
	{
		var integerValue = PropertyValue.FromInt(1);
		var floatValue = PropertyValue.FromFloat(1f);

		integerValue.Equals(floatValue).Should().BeFalse();
		floatValue.Equals(integerValue).Should().BeFalse();
	}

	[Fact]
	public void StepEquals_SameKeyNumberDifferentValueType_AreNotEqual()
	{
		var first = BuildStep(1, ("setpoint", PropertyValue.FromInt(1)));
		var second = BuildStep(1, ("setpoint", PropertyValue.FromFloat(1f)));

		first.Equals(second).Should().BeFalse();
	}

	private static Recipe DeepCopy(Recipe recipe)
	{
		var copiedSteps = recipe.Steps
			.Select(step => new Step(step.ActionKey, CopyProperties(step.Properties)))
			.ToImmutableList();

		return new Recipe(copiedSteps);
	}

	private static ImmutableDictionary<PropertyId, PropertyValue> CopyProperties(
		ImmutableDictionary<PropertyId, PropertyValue> properties)
	{
		var builder = ImmutableDictionary.CreateBuilder<PropertyId, PropertyValue>();
		foreach (var pair in properties)
		{
			builder[pair.Key] = pair.Value;
		}

		return builder.ToImmutable();
	}
}
