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
	public void EmptyRecipe_IsValid_NoWarnings()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);

		driver.IsValid.Should().BeTrue();
		driver.Warnings.Should().BeEmpty();
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
	public void UnclosedLoop_BlocksValidity()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);
		driver.AddFor(3).AddWait(10f);

		driver.IsValid.Should().BeFalse("unclosed loops are structural defects that block validity");
		driver.Warnings.Should().ContainSingle(w => w.Contains("Unclosed For loop", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void Apply_AcceptsDefectiveSnapshot_KeepsIsValidFalse()
	{
		fixture.Session.Reset();
		var driver = new RecipeTestDriver(fixture.Session);

		var applyResult = fixture.Session.AppendStep(RecipeTestDriver.ForLoopActionId);

		applyResult.IsSuccess.Should().BeTrue("a defective snapshot (unclosed For) is still accepted to let the user keep editing");
		driver.IsValid.Should().BeFalse("an unclosed For loop is a structural defect");
		driver.Warnings.Should().NotBeEmpty();
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
