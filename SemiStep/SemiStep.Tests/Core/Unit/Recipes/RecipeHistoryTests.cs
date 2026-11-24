using AwesomeAssertions;

using SemiStep.Core.Recipes;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Recipes;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Area", "History")]
public sealed class RecipeHistoryTests
{
	[Fact]
	public void CommitRedo_AfterCommitUndo_RestoresBothStacks()
	{
		var history = new RecipeHistory();
		var older = Recipe.Empty;
		var newer = Recipe.Empty.AppendStep(new Step(1, []));
		history.Push(older);

		history.CommitUndo(newer);

		history.UndoCount.Should().Be(0);
		history.PeekRedo().Should().BeSameAs(newer);

		history.CommitRedo(older);

		history.UndoCount.Should().Be(1);
		history.PeekUndo().Should().BeSameAs(older);
		history.CanRedo.Should().BeFalse();
	}
}
