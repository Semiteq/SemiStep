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
}
