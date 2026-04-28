using FluentAssertions;

using Tests.Core.Helpers;

using Xunit;

namespace Tests.Core.Integration.Mutation;

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Area", "MutationEdgeCases")]
public sealed class CoreMutationEdgeCasesTests(CoreFixture fixture) : IClassFixture<CoreFixture>
{
	private const int InvalidNegativeIndex = -1;
	private const int InvalidLargeIndex = 100;

	[Fact]
	public void RemoveStep_NegativeIndex_Fails()
	{
		fixture.Workspace.Reset();
		var driver = new RecipeTestDriver(fixture.Workspace, fixture.Editor);
		driver.AddWait();

		var result = fixture.Editor.RemoveStep(InvalidNegativeIndex);

		result.IsFailed.Should().BeTrue();
		driver.StepCount.Should().Be(1, "state should not change on failed mutation");
	}

	[Fact]
	public void RemoveStep_IndexBeyondCount_Fails()
	{
		fixture.Workspace.Reset();
		var driver = new RecipeTestDriver(fixture.Workspace, fixture.Editor);
		driver.AddWait();

		var result = fixture.Editor.RemoveStep(InvalidLargeIndex);

		result.IsFailed.Should().BeTrue();
		driver.StepCount.Should().Be(1, "state should not change on failed mutation");
	}

	[Fact]
	public void UpdateProperty_NonExistentColumn_Fails()
	{
		fixture.Workspace.Reset();
		var driver = new RecipeTestDriver(fixture.Workspace, fixture.Editor);
		driver.AddWait();

		var result = fixture.Editor.UpdateStepProperty(0, "non_existent_column", "123");

		result.IsFailed.Should().BeTrue("column key is not defined in the action's column list");
	}

	[Fact]
	public void UpdateProperty_TypeMismatch_Fails()
	{
		fixture.Workspace.Reset();
		var driver = new RecipeTestDriver(fixture.Workspace, fixture.Editor);
		driver.AddWait();

		var result = fixture.Editor.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "not_a_valid_number");

		result.IsFailed.Should().BeTrue("value cannot be parsed as the column's declared property type");
	}
}
