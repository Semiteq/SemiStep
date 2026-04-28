using Avalonia.Threading;

using FluentAssertions;

using FluentResults;

using SemiStep.Core.Shared;

using UI.MessageService;

using Xunit;

namespace Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "MessagePanel")]
[Trait("Category", "Unit")]
public sealed class MessagePanelViewModelTests
{
	[Fact]
	public void AddError_IncreasesErrorCountByOne()
	{
		var panel = new MessagePanelViewModel();

		panel.AddError("msg", "src");
		Dispatcher.UIThread.RunJobs(null);

		panel.ErrorCount.Should().Be(1);
	}

	[Fact]
	public void AddError_AddsEntryWithErrorSeverity()
	{
		var panel = new MessagePanelViewModel();

		panel.AddError("test error", "src");
		Dispatcher.UIThread.RunJobs(null);

		panel.Entries.Should().ContainSingle(e => e.IsError);
	}

	[Fact]
	public void AddInfo_DoesNotIncrementErrorOrWarningCount()
	{
		var panel = new MessagePanelViewModel();

		panel.AddInfo("msg", "src");
		Dispatcher.UIThread.RunJobs(null);

		panel.ErrorCount.Should().Be(0);
		panel.WarningCount.Should().Be(0);
	}

	[Fact]
	public void AddError_SetsHasErrorsTrue()
	{
		var panel = new MessagePanelViewModel();

		panel.AddError("msg", "src");
		Dispatcher.UIThread.RunJobs(null);

		panel.HasErrors.Should().BeTrue();
	}

	[Fact]
	public void HasErrors_IsFalse_WhenNoErrors()
	{
		var panel = new MessagePanelViewModel();

		panel.HasErrors.Should().BeFalse();
	}

	[Fact]
	public void HasWarnings_IsFalse_WhenNoWarnings()
	{
		var panel = new MessagePanelViewModel();

		panel.HasWarnings.Should().BeFalse();
	}

	[Fact]
	public void ErrorCountText_Singular_WhenOneError()
	{
		var panel = new MessagePanelViewModel();

		panel.AddError("msg", "src");
		Dispatcher.UIThread.RunJobs(null);

		panel.ErrorCountText.Should().Be("1 Error");
	}

	[Fact]
	public void ErrorCountText_Plural_WhenTwoErrors()
	{
		var panel = new MessagePanelViewModel();

		panel.AddError("msg1", "src");
		Dispatcher.UIThread.RunJobs(null);
		panel.AddError("msg2", "src");
		Dispatcher.UIThread.RunJobs(null);

		panel.ErrorCountText.Should().Be("2 Errors");
	}

	[Fact]
	public void WarningCountText_Singular_WhenOneWarning()
	{
		var panel = new MessagePanelViewModel();

		panel.RefreshReasons([new Warning("msg")]);
		Dispatcher.UIThread.RunJobs(null);

		panel.WarningCountText.Should().Be("1 Warning");
	}

	[Fact]
	public void StatusErrorSummary_BothErrorsAndWarnings()
	{
		var panel = new MessagePanelViewModel();

		panel.AddError("e", "src");
		Dispatcher.UIThread.RunJobs(null);
		panel.RefreshReasons([new Warning("w")]);
		Dispatcher.UIThread.RunJobs(null);

		panel.StatusErrorSummary.Should().Be("1 Error, 1 Warning");
	}

	[Fact]
	public void StatusErrorSummary_OnlyErrors()
	{
		var panel = new MessagePanelViewModel();

		panel.AddError("e1", "src");
		Dispatcher.UIThread.RunJobs(null);
		panel.AddError("e2", "src");
		Dispatcher.UIThread.RunJobs(null);

		panel.StatusErrorSummary.Should().Be("2 Errors");
	}

	[Fact]
	public void StatusErrorSummary_OnlyWarnings()
	{
		var panel = new MessagePanelViewModel();

		panel.RefreshReasons([new Warning("w")]);
		Dispatcher.UIThread.RunJobs(null);

		panel.StatusErrorSummary.Should().Be("1 Warning");
	}

	[Fact]
	public void StatusErrorSummary_Empty_WhenNone()
	{
		var panel = new MessagePanelViewModel();

		panel.StatusErrorSummary.Should().Be(string.Empty);
	}

	[Fact]
	public void Clear_ResetsCountsAndEntries()
	{
		var panel = new MessagePanelViewModel();

		panel.AddError("e", "src");
		Dispatcher.UIThread.RunJobs(null);
		panel.RefreshReasons([new Warning("w")]);
		Dispatcher.UIThread.RunJobs(null);
		panel.Clear();
		Dispatcher.UIThread.RunJobs(null);

		panel.ErrorCount.Should().Be(0);
		panel.WarningCount.Should().Be(0);
		panel.Entries.Should().BeEmpty();
	}

	[Fact]
	public void Clear_SetsHasErrorsFalse()
	{
		var panel = new MessagePanelViewModel();

		panel.AddError("e", "src");
		Dispatcher.UIThread.RunJobs(null);
		panel.Clear();
		Dispatcher.UIThread.RunJobs(null);

		panel.HasErrors.Should().BeFalse();
	}

	[Fact]
	public void HasEntries_True_AfterAddError()
	{
		var panel = new MessagePanelViewModel();

		panel.AddError("e", "src");
		Dispatcher.UIThread.RunJobs(null);

		panel.HasEntries.Should().BeTrue();
	}

	[Fact]
	public void HasEntries_False_Initially()
	{
		var panel = new MessagePanelViewModel();

		panel.HasEntries.Should().BeFalse();
	}

	[Fact]
	public void ShowPanel_True_WhenHasEntriesAndVisible()
	{
		var panel = new MessagePanelViewModel();
		panel.IsVisible = false;

		panel.AddError("e", "src");
		Dispatcher.UIThread.RunJobs(null);
		panel.IsVisible = true;
		Dispatcher.UIThread.RunJobs(null);

		panel.ShowPanel.Should().BeTrue();
	}

	[Fact]
	public void ShowPanel_False_WhenNotVisible()
	{
		var panel = new MessagePanelViewModel();

		panel.AddError("e", "src");
		Dispatcher.UIThread.RunJobs(null);
		panel.IsVisible = false;
		Dispatcher.UIThread.RunJobs(null);

		panel.ShowPanel.Should().BeFalse();
	}

	[Fact]
	public void RefreshReasons_AddsStructuralErrors()
	{
		var panel = new MessagePanelViewModel();
		List<IReason> reasons = [new Error("some error")];

		panel.RefreshReasons(reasons);
		Dispatcher.UIThread.RunJobs(null);

		panel.Entries.Should().ContainSingle(e => e.IsStructural && e.IsError);
	}

	[Fact]
	public void RefreshReasons_AddsStructuralWarnings()
	{
		var panel = new MessagePanelViewModel();
		List<IReason> reasons = [new Warning("some warning")];

		panel.RefreshReasons(reasons);
		Dispatcher.UIThread.RunJobs(null);

		panel.Entries.Should().ContainSingle(e => e.IsStructural && e.IsWarning);
	}

	[Fact]
	public void RefreshReasons_RemovesOldStructuralEntries_BeforeAddingNew()
	{
		var panel = new MessagePanelViewModel();

		panel.RefreshReasons([new Error("old error")]);
		Dispatcher.UIThread.RunJobs(null);
		panel.RefreshReasons([]);
		Dispatcher.UIThread.RunJobs(null);

		panel.Entries.Should().BeEmpty();
	}

	[Fact]
	public void RefreshReasons_PreservesNonStructuralEntries()
	{
		var panel = new MessagePanelViewModel();

		panel.AddError("non-structural", "custom source");
		Dispatcher.UIThread.RunJobs(null);
		panel.RefreshReasons([]);
		Dispatcher.UIThread.RunJobs(null);

		panel.Entries.Should().ContainSingle(e => !e.IsStructural);
	}

	[Fact]
	public void ClearCommand_RemovesNonStructuralEntries()
	{
		var panel = new MessagePanelViewModel();

		panel.AddError("e", "src");
		Dispatcher.UIThread.RunJobs(null);
		panel.ClearCommand.Execute().Subscribe();
		Dispatcher.UIThread.RunJobs(null);

		panel.Entries.Should().BeEmpty();
	}

	[Fact]
	public void ClearCommand_PreservesStructuralEntries()
	{
		var panel = new MessagePanelViewModel();

		panel.RefreshReasons([new Error("structural error")]);
		Dispatcher.UIThread.RunJobs(null);
		panel.ClearCommand.Execute().Subscribe();
		Dispatcher.UIThread.RunJobs(null);

		panel.Entries.Should().ContainSingle(e => e.IsStructural);
	}
}
