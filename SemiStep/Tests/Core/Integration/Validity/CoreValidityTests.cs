using Domain.State;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using Shared.Reasons;

using Tests.Core.Helpers;

using Xunit;

namespace Tests.Core.Integration.Validity;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "Validity")]
public sealed class CoreValidityTests
{
	[Fact]
	public async Task EmptyRecipe_IsValid_ButHasWarning()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);

		// Empty recipe is valid but should have a warning
		driver.IsValid.Should().BeTrue();
		driver.Snapshot.Warnings.Should().NotBeEmpty();
		driver.Snapshot.Warnings.Should().ContainSingle(w => w is EmptyRecipeWarning);
	}

	[Fact]
	public async Task ValidRecipe_NoErrors()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddWait(10f).AddWait(20f);

		driver.IsValid.Should().BeTrue();
		driver.Snapshot.Errors.Should().BeEmpty();
	}

	[Fact]
	public async Task RecipeWithClosedLoop_IsValid()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddFor(3).AddWait(10f).AddEndFor();

		driver.IsValid.Should().BeTrue();
		driver.Snapshot.Errors.Should().BeEmpty();
	}

	[Fact]
	public async Task BrokenLoop_HasLoopIntegrityError()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddFor(3).AddWait(10f); // No EndFor

		driver.IsValid.Should().BeFalse();
		driver.Snapshot.Errors.Should().ContainSingle(e => e is LoopIntegrityError);
	}

	[Fact]
	public async Task MaxDepthExceeded_HasLoopNestingDepthError()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);

		// Create 4 nested loops (exceeds max depth of 3)
		driver.AddFor(1).AddFor(1).AddFor(1).AddFor(1);
		driver.AddWait(1f);
		driver.AddEndFor().AddEndFor().AddEndFor().AddEndFor();

		driver.IsValid.Should().BeFalse();
		driver.Snapshot.Errors.Should().ContainSingle(e => e is LoopNestingDepthError);
	}

	[Fact]
	public async Task IsValid_DerivedFromReasons()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);
		driver.AddWait(10f);

		// Valid recipe should have no errors
		driver.Snapshot.Errors.Should().BeEmpty();
		driver.Snapshot.IsValid.Should().BeTrue();

		// Break the recipe
		driver.AddFor(2);

		// Invalid recipe should have errors
		driver.Snapshot.Errors.Should().NotBeEmpty();
		driver.Snapshot.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task WarningsDoNotAffectValidity()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);

		// Empty recipe has warnings but is still valid
		driver.Snapshot.Warnings.Should().NotBeEmpty();
		driver.IsValid.Should().BeTrue();
	}

	[Fact]
	public async Task MultipleErrors_AllCaptured()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync();
		using var _ = services as IDisposable;

		var driver = new RecipeTestDriver(facade);

		// Create multiple unclosed loops
		driver.AddFor(1).AddFor(1).AddWait(5f);

		driver.IsValid.Should().BeFalse();
		driver.Snapshot.Errors.Should().HaveCountGreaterThanOrEqualTo(2);
	}
}
