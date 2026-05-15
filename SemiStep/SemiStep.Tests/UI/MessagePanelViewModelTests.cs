using Avalonia.Headless.XUnit;

using FluentAssertions;

using FluentResults;

using SemiStep.Core.Shared;

using SemiStep.UI.MessageService;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "MessagePanel")]
[Trait("Category", "Unit")]
public sealed class MessagePanelViewModelTests
{
	[AvaloniaFact]
	public void FreshPanel_HasCleanInitialState()
	{
		var panel = new MessagePanelViewModel();

		panel.HasErrors.Should().BeFalse();
		panel.HasWarnings.Should().BeFalse();
		panel.HasEntries.Should().BeFalse();
		panel.ErrorCount.Should().Be(0);
		panel.WarningCount.Should().Be(0);
		panel.StatusErrorSummary.Should().Be(string.Empty);
	}

	[AvaloniaFact]
	public void AddError_IncrementsCount_AddsErrorEntry_SetsHasErrorsAndHasEntries()
	{
		var panel = new MessagePanelViewModel();

		panel.AddError("msg", "src");

		panel.ErrorCount.Should().Be(1);
		panel.HasErrors.Should().BeTrue();
		panel.HasEntries.Should().BeTrue();
		panel.Entries.Should().ContainSingle(e => e.IsError);
	}

	[AvaloniaFact]
	public void AddInfo_DoesNotIncrementErrorOrWarningCount()
	{
		var panel = new MessagePanelViewModel();

		panel.AddInfo("msg", "src");

		panel.ErrorCount.Should().Be(0);
		panel.WarningCount.Should().Be(0);
	}

	[AvaloniaTheory]
	[InlineData(1, "1 Error")]
	[InlineData(2, "2 Errors")]
	public void ErrorCountText_UsesSingularOrPlural(int errorCount, string expected)
	{
		var panel = new MessagePanelViewModel();

		for (var i = 0; i < errorCount; i++)
		{
			panel.AddError($"msg{i}", "src");
		}

		panel.ErrorCountText.Should().Be(expected);
	}

	[AvaloniaTheory]
	[InlineData(1, 1, "1 Error, 1 Warning")]
	[InlineData(2, 0, "2 Errors")]
	[InlineData(0, 1, "1 Warning")]
	public void StatusErrorSummary_CombinesErrorsAndWarnings(int errorCount, int warningCount, string expected)
	{
		var panel = new MessagePanelViewModel();

		for (var i = 0; i < errorCount; i++)
		{
			panel.AddError($"e{i}", "src");
		}
		if (warningCount > 0)
		{
			var warnings = Enumerable.Range(0, warningCount).Select(IReason (i) => new Warning($"w{i}")).ToList();
			panel.RefreshReasons(warnings);
		}

		panel.StatusErrorSummary.Should().Be(expected);
	}

	[AvaloniaFact]
	public void Clear_ResetsCountsEntriesAndFlags()
	{
		var panel = new MessagePanelViewModel();

		panel.AddError("e", "src");
		panel.RefreshReasons([new Warning("w")]);
		panel.Clear();

		panel.ErrorCount.Should().Be(0);
		panel.WarningCount.Should().Be(0);
		panel.HasErrors.Should().BeFalse();
		panel.Entries.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void ShowPanel_True_WhenHasEntriesAndVisible()
	{
		var panel = new MessagePanelViewModel();
		panel.IsVisible = false;

		panel.AddError("e", "src");
		panel.IsVisible = true;

		panel.ShowPanel.Should().BeTrue();
	}

	[AvaloniaFact]
	public void ShowPanel_False_WhenNotVisible()
	{
		var panel = new MessagePanelViewModel();

		panel.AddError("e", "src");
		panel.IsVisible = false;

		panel.ShowPanel.Should().BeFalse();
	}

	[AvaloniaTheory]
	[InlineData(true)]
	[InlineData(false)]
	public void RefreshReasons_AddsStructuralEntries_PreservingSeverity(bool isError)
	{
		var panel = new MessagePanelViewModel();
		List<IReason> reasons = isError ? [new Error("some error")] : [new Warning("some warning")];

		panel.RefreshReasons(reasons);

		panel.Entries.Should().ContainSingle(e => e.IsStructural && (isError ? e.IsError : e.IsWarning));
	}

	[AvaloniaFact]
	public void RefreshReasons_RemovesOldStructuralEntries_BeforeAddingNew()
	{
		var panel = new MessagePanelViewModel();

		panel.RefreshReasons([new Error("old error")]);
		panel.RefreshReasons([]);

		panel.Entries.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void RefreshReasons_PreservesNonStructuralEntries()
	{
		var panel = new MessagePanelViewModel();

		panel.AddError("non-structural", "custom source");
		panel.RefreshReasons([]);

		panel.Entries.Should().ContainSingle(e => !e.IsStructural);
	}

	[AvaloniaFact]
	public void ClearCommand_RemovesNonStructuralEntries()
	{
		var panel = new MessagePanelViewModel();

		panel.AddError("e", "src");
		panel.ClearCommand.Execute().Subscribe();

		panel.Entries.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void ClearCommand_PreservesStructuralEntries()
	{
		var panel = new MessagePanelViewModel();

		panel.RefreshReasons([new Error("structural error")]);
		panel.ClearCommand.Execute().Subscribe();

		panel.Entries.Should().ContainSingle(e => e.IsStructural);
	}
}
