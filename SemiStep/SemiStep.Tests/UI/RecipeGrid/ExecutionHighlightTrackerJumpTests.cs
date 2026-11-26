using System.Collections.ObjectModel;
using System.ComponentModel;

using Avalonia.Headless.XUnit;

using AwesomeAssertions;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "ExecutionHighlight")]
[Trait("Category", "Unit")]
public sealed class ExecutionHighlightTrackerJumpTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();

	public ValueTask InitializeAsync()
	{
		return _fixture.InitializeAsync();
	}

	public ValueTask DisposeAsync()
	{
		return _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void InitialActualLine_MarksPastAndCurrent()
	{
		var rows = BuildRows(10);
		var tracker = new ExecutionHighlightTracker(() => rows.Count, i => rows[i]);

		tracker.OnExecutionStateChanged(new PlcExecutionInfo(RecipeActive: true, ActualLine: 5, StepCurrentTime: 0f, ForLoopCount1: 0, ForLoopCount2: 0, ForLoopCount3: 0));

		for (var i = 0; i < 5; i++)
		{
			rows[i].IsPastStep.Should().BeTrue($"row {i} should be past");
			rows[i].IsCurrentStep.Should().BeFalse();
		}

		rows[5].IsCurrentStep.Should().BeTrue();
		rows[5].IsPastStep.Should().BeFalse();

		for (var i = 6; i < 10; i++)
		{
			rows[i].IsPastStep.Should().BeFalse();
			rows[i].IsCurrentStep.Should().BeFalse();
		}
	}

	[AvaloniaFact]
	public void ForwardJump_AdvancesPastAndCurrentFlags()
	{
		var rows = BuildRows(10);
		var tracker = new ExecutionHighlightTracker(() => rows.Count, i => rows[i]);

		tracker.OnExecutionStateChanged(BuildActive(2));
		tracker.OnExecutionStateChanged(BuildActive(7));

		for (var i = 0; i < 7; i++)
		{
			rows[i].IsPastStep.Should().BeTrue();
			rows[i].IsCurrentStep.Should().BeFalse();
		}

		rows[7].IsCurrentStep.Should().BeTrue();
		rows[7].IsPastStep.Should().BeFalse();

		for (var i = 8; i < 10; i++)
		{
			rows[i].IsPastStep.Should().BeFalse();
			rows[i].IsCurrentStep.Should().BeFalse();
		}
	}

	[AvaloniaFact]
	public void BackwardJump_ClearsStalePastFlags()
	{
		var rows = BuildRows(10);
		var tracker = new ExecutionHighlightTracker(() => rows.Count, i => rows[i]);

		tracker.OnExecutionStateChanged(BuildActive(7));
		tracker.OnExecutionStateChanged(BuildActive(3));

		for (var i = 0; i < 3; i++)
		{
			rows[i].IsPastStep.Should().BeTrue();
			rows[i].IsCurrentStep.Should().BeFalse();
		}

		rows[3].IsCurrentStep.Should().BeTrue();
		rows[3].IsPastStep.Should().BeFalse();

		for (var i = 4; i <= 7; i++)
		{
			rows[i].IsPastStep.Should().BeFalse($"row {i} stale past flag must be cleared");
			rows[i].IsCurrentStep.Should().BeFalse();
		}
	}

	[AvaloniaFact]
	public void RecipeActiveTransitionsToFalse_KeepsPastTint_AndMarksTheStopRow()
	{
		var rows = BuildRows(10);
		var tracker = new ExecutionHighlightTracker(() => rows.Count, i => rows[i]);

		tracker.OnExecutionStateChanged(BuildActive(5));
		tracker.OnExecutionStateChanged(new PlcExecutionInfo(RecipeActive: false, ActualLine: 0, StepCurrentTime: 0f, ForLoopCount1: 0, ForLoopCount2: 0, ForLoopCount3: 0));

		rows.Should().OnlyContain(row => !row.IsCurrentStep);
		rows.Take(6).Should().OnlyContain(row => row.IsPastStep);
		rows.Skip(6).Should().OnlyContain(row => !row.IsPastStep);
		rows.Select(row => row.IsStopStep).Should().Equal(false, false, false, false, false, true, false, false, false, false);
	}

	[AvaloniaFact]
	public void ClearRunTintsWhenIdle_DuringARun_ChangesNothing()
	{
		var rows = BuildRows(10);
		var tracker = new ExecutionHighlightTracker(() => rows.Count, i => rows[i]);
		tracker.OnExecutionStateChanged(BuildActive(5));

		tracker.ClearRunTintsWhenIdle();

		rows[5].IsCurrentStep.Should().BeTrue();
		rows.Take(5).Should().OnlyContain(row => row.IsPastStep);
	}

	[AvaloniaFact]
	public void NoOpEvent_DoesNotRewriteProperties()
	{
		var rows = BuildRows(10);
		var tracker = new ExecutionHighlightTracker(() => rows.Count, i => rows[i]);

		tracker.OnExecutionStateChanged(BuildActive(5));

		var changeCount = 0;
		void handler(object? _1, PropertyChangedEventArgs _2)
		{
			changeCount++;
		}

		foreach (var row in rows)
		{
			row.PropertyChanged += handler;
		}

		tracker.OnExecutionStateChanged(BuildActive(5));

		changeCount.Should().Be(0, "no-op events must not trigger property writes");

		foreach (var row in rows)
		{
			row.PropertyChanged -= handler;
		}
	}

	[AvaloniaFact]
	public void ExecutionStart_ClearsAllChangedColumns()
	{
		var rows = BuildRows(10);
		rows[2].MarkChanged(["Temperature"]);
		rows[5].MarkChanged(["Pressure", "Duration"]);
		var tracker = new ExecutionHighlightTracker(() => rows.Count, i => rows[i]);

		tracker.OnExecutionStateChanged(BuildActive(0));

		foreach (var row in rows)
		{
			row.ChangedColumns.Should().BeEmpty();
		}
	}

	[AvaloniaFact]
	public void AlreadyActiveLineChange_DoesNotReClearChangedColumns()
	{
		var rows = BuildRows(10);
		var tracker = new ExecutionHighlightTracker(() => rows.Count, i => rows[i]);

		tracker.OnExecutionStateChanged(BuildActive(0));

		rows[3].MarkChanged(["Temperature"]);

		tracker.OnExecutionStateChanged(BuildActive(4));

		rows[3].ChangedColumns.Should().Contain("Temperature");
	}

	[AvaloniaFact]
	public void LineChange_RepaintsOnlyTheRowsBetweenTheOldAndTheNewLine()
	{
		var rows = BuildRows(200);
		var reads = 0;
		var tracker = new ExecutionHighlightTracker(() => rows.Count, i =>
		{
			reads++;
			return rows[i];
		});
		tracker.OnExecutionStateChanged(BuildActive(10));

		reads = 0;
		tracker.OnExecutionStateChanged(BuildActive(11));
		var forwardReads = reads;
		reads = 0;
		tracker.OnExecutionStateChanged(BuildActive(6));
		var backwardReads = reads;

		forwardReads.Should().Be(2, "a forward step repaints the old and the new running row");
		backwardReads.Should().Be(6, "a backward jump repaints rows 6 to 11");
		rows.Select(row => row.IsPastStep).Should().Equal([.. Enumerable.Range(0, 200).Select(i => i < 6)]);
		rows.Select(row => row.IsCurrentStep).Should().Equal([.. Enumerable.Range(0, 200).Select(i => i == 6)]);
	}

	[AvaloniaFact]
	public void LineOutsideTheRows_RepaintsWithoutReadingPastTheEnds()
	{
		var rows = BuildRows(5);
		var tracker = new ExecutionHighlightTracker(() => rows.Count, i => rows[i]);

		tracker.OnExecutionStateChanged(BuildActive(-1));
		tracker.OnExecutionStateChanged(BuildActive(2));
		tracker.OnExecutionStateChanged(BuildActive(5));

		rows.Should().OnlyContain(row => row.IsPastStep && !row.IsCurrentStep);

		tracker.OnExecutionStateChanged(BuildActive(-1));

		rows.Should().OnlyContain(row => !row.IsPastStep && !row.IsCurrentStep);
	}

	private static PlcExecutionInfo BuildActive(int actualLine)
	{
		return new PlcExecutionInfo(
			RecipeActive: true,
			ActualLine: actualLine,
			StepCurrentTime: 0f,
			ForLoopCount1: 0,
			ForLoopCount2: 0,
			ForLoopCount3: 0);
	}

	private ObservableCollection<RecipeRowViewModel> BuildRows(int count)
	{
		var rows = new ObservableCollection<RecipeRowViewModel>();
		var action = _fixture.RecipeMetadataRegistry.GetAction(RecipeTestDriver.WaitActionId).Value;
		for (var i = 0; i < count; i++)
		{
			var step = new Step(RecipeTestDriver.WaitActionId, []);
			rows.Add(new RecipeRowViewModel(
				i + 1, step, action, _fixture.RecipeMetadataRegistry, _fixture.ComboBoxItemsProvider,
				new HashSet<string>()));
		}

		return rows;
	}
}
