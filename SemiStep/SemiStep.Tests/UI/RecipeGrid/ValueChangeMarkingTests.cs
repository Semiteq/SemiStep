using System.Collections.Immutable;

using Avalonia.Headless.XUnit;

using AwesomeAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "ChangeHighlight")]
[Trait("Category", "Integration")]
public sealed class ValueChangeMarkingTests : IAsyncLifetime
{
	private const string NestedConfig = "Standalone/NestedActionsValid";
	private const int BranchingActionId = 300;
	private const string SelectorColumn = "branch_sel";
	private const string SubValueColumn = "sub_value";
	private const string SubNoDefaultColumn = "sub_nodefault";
	private const string ManualValue = "1";

	private readonly UIFixture _fixture = new();
	private CanonicalRecipeGridSurface _canonical = null!;
	private TransposedRecipeGridSurface _transposed = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.Coordinator.NewRecipe();
		(_canonical, _transposed) = CreateSurfaces(_fixture);
	}

	public ValueTask DisposeAsync()
	{
		return _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void ActionChange_ResetsATypedComment_AndMarksIt_OnBothSurfaces()
	{
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.CommentColumn, "abc");

		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		_fixture.Coordinator.CurrentRecipe.Steps[0].Properties[new PropertyId(RecipeTestDriver.CommentColumn)]
			.AsString().Should().BeEmpty();
		foreach (var row in BothRows(0))
		{
			row.ChangedColumns.Should().BeEquivalentTo([RecipeTestDriver.CommentColumn, RecipeTestDriver.TaskColumn]);
		}
	}

	[AvaloniaFact]
	public void ActionChange_LeavesACellEmptyBeforeAndAfterUnmarked_OnBothSurfaces()
	{
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		foreach (var row in BothRows(0))
		{
			row.ChangedColumns.Should().NotContain(RecipeTestDriver.CommentColumn);
			row.ChangedColumns.Should().NotContain(RecipeTestDriver.StepDurationColumn, "the new action drops the key");
		}
	}

	[AvaloniaFact]
	public void ActionChange_MarksANumericCellWhoseValueChanged_OnBothSurfaces()
	{
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.StepDurationColumn, "25");

		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.WithGroupActionId);

		foreach (var row in BothRows(0))
		{
			row.ChangedColumns.Should().BeEquivalentTo([RecipeTestDriver.StepDurationColumn, RecipeTestDriver.TargetColumn]);
		}
	}

	[AvaloniaFact]
	public void ActionChange_LeavesANumericCellWithAnEqualValueUnmarked_OnBothSurfaces()
	{
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.WithGroupActionId);

		foreach (var row in BothRows(0))
		{
			row.ChangedColumns.Should().BeEquivalentTo([RecipeTestDriver.TargetColumn]);
		}
	}

	[AvaloniaFact]
	public void ActionChange_OnACsvStepWithoutTheCommentKey_LeavesTheCommentUnmarked_OnBothSurfaces()
	{
		InsertCsvStepWithoutComment();

		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);

		foreach (var row in BothRows(0))
		{
			row.ChangedColumns.Should().BeEquivalentTo([RecipeTestDriver.TaskColumn]);
		}
	}

	[AvaloniaFact]
	public void ClearAll_RemovesTheWipedCommentMark_AndTurnsHasMarksOff()
	{
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		_fixture.Coordinator.UpdateStepProperty(0, RecipeTestDriver.CommentColumn, "abc");
		_fixture.Coordinator.ChangeStepAction(0, RecipeTestDriver.ForLoopActionId);
		var hasMarks = false;
		using var subscription = _canonical.HasChangedMarks.Subscribe(value => hasMarks = value);
		hasMarks.Should().BeTrue("precondition: the action change left marks");

		_canonical.ClearAllChangedMarks();
		_transposed.ClearAllChangedMarks();

		BothRows(0).Should().OnlyContain(row => row.ChangedColumns.Count == 0);
		hasMarks.Should().BeFalse();
	}

	[AvaloniaFact]
	public async Task SelectorChange_MarksOnlyTheCellsWhoseValueChanged_OnBothSurfaces()
	{
		var nested = new UIFixture();
		await nested.InitializeAsync(NestedConfig);
		try
		{
			var (canonical, transposed) = CreateSurfaces(nested);
			nested.Coordinator.AppendStep(BranchingActionId);
			canonical.RecipeRows[0].SetPropertyValue(RecipeTestDriver.CommentColumn, "note");

			canonical.RecipeRows[0].SetPropertyValue(SelectorColumn, ManualValue);

			foreach (var row in new[] { canonical.RecipeRows[0], transposed.StepColumns[0].Row })
			{
				row.ChangedColumns.Should().BeEquivalentTo([SubValueColumn, SubNoDefaultColumn]);
			}
		}
		finally
		{
			await nested.DisposeAsync();
		}
	}

	[AvaloniaFact]
	public async Task SelectorChange_OnALoadedStepHoldingAHiddenValue_MarksTheReseededCell_OnBothSurfaces()
	{
		var nested = new UIFixture();
		await nested.InitializeAsync(NestedConfig);
		try
		{
			var loaded = new Step(
				BranchingActionId,
				ImmutableDictionary<PropertyId, PropertyValue>.Empty
					.Add(new PropertyId(SelectorColumn), PropertyValue.FromInt(0))
					.Add(new PropertyId(RecipeTestDriver.CommentColumn), PropertyValue.FromString(string.Empty))
					.Add(new PropertyId(SubValueColumn), PropertyValue.FromFloat(99f)));
			nested.Session.LoadAsCurrent(Recipe.Empty.AppendStep(loaded)).IsSuccess.Should().BeTrue();
			var (canonical, transposed) = CreateSurfaces(nested);

			canonical.RecipeRows[0].SetPropertyValue(SelectorColumn, ManualValue);

			nested.Coordinator.CurrentRecipe.Steps[0].Properties[new PropertyId(SubValueColumn)]
				.AsFloat().Should().Be(50f, "precondition: the selector change reseeds the hidden value");
			foreach (var row in new[] { canonical.RecipeRows[0], transposed.StepColumns[0].Row })
			{
				row.ChangedColumns.Should().BeEquivalentTo([SubValueColumn, SubNoDefaultColumn]);
			}
		}
		finally
		{
			await nested.DisposeAsync();
		}
	}

	[AvaloniaFact]
	public async Task SelectorSet_OnACsvStepWithoutTheSelectorKey_LeavesTheSelectorUnmarked_OnBothSurfaces()
	{
		var nested = new UIFixture();
		await nested.InitializeAsync(NestedConfig);
		try
		{
			var csvStep = new Step(
				BranchingActionId,
				ImmutableDictionary<PropertyId, PropertyValue>.Empty
					.Add(new PropertyId(RecipeTestDriver.CommentColumn), PropertyValue.FromString("note")));
			nested.Session.LoadAsCurrent(Recipe.Empty.AppendStep(csvStep)).IsSuccess.Should().BeTrue();
			var (canonical, transposed) = CreateSurfaces(nested);

			canonical.RecipeRows[0].SetPropertyValue(SelectorColumn, ManualValue);

			nested.Coordinator.CurrentRecipe.Steps[0].Properties[new PropertyId(SelectorColumn)]
				.AsInt().Should().Be(1, "precondition: the operator set the selector");
			foreach (var row in new[] { canonical.RecipeRows[0], transposed.StepColumns[0].Row })
			{
				row.ChangedColumns.Should().BeEquivalentTo([SubValueColumn, SubNoDefaultColumn]);
			}
		}
		finally
		{
			await nested.DisposeAsync();
		}
	}

	[AvaloniaFact]
	public void MirroredUpdate_AddingAnEmptyStringKey_DoesNotMarkIt()
	{
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var row = _canonical.RecipeRows[0];
		var withoutComment = WithoutComment(row.CurrentStep);
		row.UpdateStep(withoutComment);

		RecipeRowUpdateSynchronizer.ApplyPropertyUpdate(
			row,
			withoutComment.WithProperty(RecipeTestDriver.CommentColumn, PropertyValue.FromString(string.Empty)));

		row.ChangedColumns.Should().NotContain(RecipeTestDriver.CommentColumn);
	}

	[AvaloniaFact]
	public void MirroredUpdate_AddingANonEmptyKey_MarksIt()
	{
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var row = _canonical.RecipeRows[0];
		var withoutComment = WithoutComment(row.CurrentStep);
		row.UpdateStep(withoutComment);

		RecipeRowUpdateSynchronizer.ApplyPropertyUpdate(
			row,
			withoutComment.WithProperty(RecipeTestDriver.CommentColumn, PropertyValue.FromString("seeded")));

		row.ChangedColumns.Should().Contain(RecipeTestDriver.CommentColumn);
	}

	[AvaloniaFact]
	public void FirstTypedComment_OnACsvStepWithoutTheKey_IsMarkedOnNeitherSurface()
	{
		InsertCsvStepWithoutComment();

		_canonical.RecipeRows[0].SetPropertyValue(RecipeTestDriver.CommentColumn, "first note");

		_fixture.Coordinator.CurrentRecipe.Steps[0].Properties[new PropertyId(RecipeTestDriver.CommentColumn)]
			.AsString().Should().Be("first note");
		foreach (var row in BothRows(0))
		{
			row.ChangedColumns.Should().NotContain(RecipeTestDriver.CommentColumn, "an operator value is an edit");
		}
	}

	private static (CanonicalRecipeGridSurface Canonical, TransposedRecipeGridSurface Transposed) CreateSurfaces(
		UIFixture fixture)
	{
		var canonical = fixture.CreateCanonicalSurface();
		canonical.Initialize();
		var transposed = fixture.CreateTransposedSurface();
		transposed.Initialize();
		return (canonical, transposed);
	}

	private void InsertCsvStepWithoutComment()
	{
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var csvStep = WithoutComment(_fixture.Coordinator.CurrentRecipe.Steps[0]);
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.InsertSteps(0, [csvStep]);
		_fixture.Coordinator.CurrentRecipe.Steps[0].Properties
			.Should().NotContainKey(new PropertyId(RecipeTestDriver.CommentColumn), "precondition: a CSV blank cell has no key");
	}

	private IEnumerable<RecipeRowViewModel> BothRows(int stepIndex)
	{
		return [_canonical.RecipeRows[stepIndex], _transposed.StepColumns[stepIndex].Row];
	}

	private static Step WithoutComment(Step step)
	{
		return step with { Properties = step.Properties.Remove(new PropertyId(RecipeTestDriver.CommentColumn)) };
	}
}
