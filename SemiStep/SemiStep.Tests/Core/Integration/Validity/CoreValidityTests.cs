using FluentAssertions;

using SemiStep.Tests.Core.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Integration.Validity;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "Validity")]
public sealed class CoreValidityTests(CoreFixture fixture) : IClassFixture<CoreFixture>
{
	[Fact]
	public void EmptyRecipe_IsValid_ButHasWarning()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);

		driver.IsValid.Should().BeTrue();
		driver.Warnings.Should().NotBeEmpty();
		driver.Warnings.Should().ContainSingle(w => w.Contains("no steps", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void ValidRecipe_NoErrors()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait(10f).AddWait(20f);

		driver.IsValid.Should().BeTrue();
		driver.Errors.Should().BeEmpty();
	}

	[Fact]
	public void RecipeWithClosedLoop_IsValid()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddFor(3).AddWait(10f).AddEndFor();

		driver.IsValid.Should().BeTrue();
		driver.Errors.Should().BeEmpty();
	}

	[Fact]
	public void UnclosedLoop_ProducesWarning()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddFor(3).AddWait(10f);

		driver.IsValid.Should().BeTrue("unclosed loops are warnings, not errors");
		driver.Warnings.Should().ContainSingle(w => w.Contains("Unclosed For loop", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void MaxDepth3Exceeded_RejectsMutation()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);

		driver.AddFor(1).AddFor(1).AddFor(1).AddFor(1).AddWait(1f);

		var stepCountBeforeRejection = driver.StepCount;
		var result = fixture.Session.AppendStep(RecipeTestDriver.EndForLoopActionId);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainSingle(e => e.Message.Contains("nesting depth", StringComparison.OrdinalIgnoreCase));
		driver.IsValid.Should().BeTrue("the mutation was rejected, recipe is unchanged");
		driver.StepCount.Should().Be(stepCountBeforeRejection);
	}

	[Fact]
	public void ExceedingMaxDepth_RejectsMutation_AndRecipeRemainsValid()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddWait(10f);

		driver.Errors.Should().BeEmpty();
		driver.IsValid.Should().BeTrue();

		driver.AddFor(1).AddFor(1).AddFor(1).AddFor(1).AddWait(1f);
		var stepCountBeforeRejection = driver.StepCount;

		var result = fixture.Session.AppendStep(RecipeTestDriver.EndForLoopActionId);

		result.IsFailed.Should().BeTrue("exceeding max loop depth produces an error");
		driver.IsValid.Should().BeTrue("the mutation was rejected, recipe is unchanged");
		driver.StepCount.Should().Be(stepCountBeforeRejection);
	}

	[Fact]
	public void WarningsDoNotAffectValidity()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);

		driver.Warnings.Should().NotBeEmpty();
		driver.IsValid.Should().BeTrue("warnings alone should not invalidate the recipe");
	}

	[Fact]
	public void MultipleWarnings_AllCaptured()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);

		driver.AddFor(1).AddFor(1).AddWait(5f);

		driver.Warnings.Should().HaveCountGreaterThanOrEqualTo(2,
			"two unclosed For loops should produce at least two warnings");
	}
}
