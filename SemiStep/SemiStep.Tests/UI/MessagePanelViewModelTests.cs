using Avalonia.Headless.XUnit;

using AwesomeAssertions;

using FluentResults;

using SemiStep.Core.Recipes.Import.Warnings;
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
	public void StatusErrorSummary_ErrorsOnly_IsErrorCountText()
	{
		var panel = new MessagePanelViewModel();

		panel.RefreshReasons([new Error("e1"), new Error("e2")]);

		panel.StatusErrorSummary.Should().Be(panel.ErrorCountText);
	}

	[AvaloniaFact]
	public void StatusErrorSummary_WarningsOnly_IsWarningCountText()
	{
		var panel = new MessagePanelViewModel();

		panel.RefreshReasons([new Warning("w1")]);

		panel.StatusErrorSummary.Should().Be(panel.WarningCountText);
	}

	[AvaloniaFact]
	public void StatusErrorSummary_ErrorsAndWarnings_StartsWithErrorsAndEndsWithWarnings()
	{
		var panel = new MessagePanelViewModel();

		panel.RefreshReasons([new Error("e1"), new Warning("w1")]);

		panel.StatusErrorSummary.Should().StartWith(panel.ErrorCountText).And.EndWith(panel.WarningCountText);
	}

	[AvaloniaFact]
	public void ShowPanel_True_WhenHasEntriesAndVisible()
	{
		var panel = new MessagePanelViewModel
		{
			IsVisible = false
		};

		panel.RefreshReasons([new Error("e")]);
		panel.IsVisible = true;

		panel.ShowPanel.Should().BeTrue();
	}

	[AvaloniaFact]
	public void ShowPanel_False_WhenNotVisible()
	{
		var panel = new MessagePanelViewModel();

		panel.RefreshReasons([new Error("e")]);
		panel.IsVisible = false;

		panel.ShowPanel.Should().BeFalse();
	}

	[AvaloniaFact]
	public void RefreshReasons_Error_AddsErrorEntry()
	{
		var panel = new MessagePanelViewModel();

		panel.RefreshReasons([new Error("some error")]);

		panel.Entries.Should().ContainSingle(e => e.IsError);
	}

	[AvaloniaFact]
	public void RefreshReasons_Warning_AddsWarningEntry()
	{
		var panel = new MessagePanelViewModel();

		panel.RefreshReasons([new Warning("some warning")]);

		panel.Entries.Should().ContainSingle(e => e.IsWarning);
	}

	[AvaloniaFact]
	public void RefreshReasons_ReplacesPreviousEntries()
	{
		var panel = new MessagePanelViewModel();

		panel.RefreshReasons([new Error("first")]);
		panel.RefreshReasons([new Warning("second")]);

		panel.Entries.Should().ContainSingle(e => e.IsWarning);
		panel.ErrorCount.Should().Be(0);
		panel.WarningCount.Should().Be(1);
	}

	[AvaloniaFact]
	public void RefreshReasons_KeepsOperationEntryFirst()
	{
		var panel = new MessagePanelViewModel();

		panel.ReportWarning("operation warning");
		panel.RefreshReasons([new Error("validation error")]);

		panel.Entries.Should().HaveCount(2);
		panel.Entries[0].Severity.Should().Be(MessageSeverity.Warning);
		panel.Entries[1].Severity.Should().Be(MessageSeverity.Error);
	}

	[AvaloniaFact]
	public void ReportOperation_ShowsAsRow_LatestOnly()
	{
		var panel = new MessagePanelViewModel();

		panel.ReportError("first operation");
		panel.ReportWarning("second operation");

		panel.Entries.Should().ContainSingle().Which.Severity.Should().Be(MessageSeverity.Warning);
	}

	[AvaloniaFact]
	public void ReportSuccess_MapsToInfoSeverity()
	{
		var panel = new MessagePanelViewModel();

		panel.ReportSuccess("saved");

		panel.Entries.Should().ContainSingle().Which.Severity.Should().Be(MessageSeverity.Info);
	}

	[AvaloniaFact]
	public void ReportWarning_MapsToWarningSeverity()
	{
		var panel = new MessagePanelViewModel();

		panel.ReportWarning("careful");

		panel.Entries.Should().ContainSingle().Which.Severity.Should().Be(MessageSeverity.Warning);
	}

	[AvaloniaFact]
	public void ReportError_MapsToErrorSeverity()
	{
		var panel = new MessagePanelViewModel();

		panel.ReportError("failed");

		panel.Entries.Should().ContainSingle().Which.Severity.Should().Be(MessageSeverity.Error);
	}

	[AvaloniaFact]
	public void Counts_IgnoreOperationEntry()
	{
		var panel = new MessagePanelViewModel();

		panel.ReportError("operation error");

		panel.ErrorCount.Should().Be(0);
		panel.WarningCount.Should().Be(0);
	}

	[AvaloniaFact]
	public void ClearOperation_RemovesOnlyOperationRow()
	{
		var panel = new MessagePanelViewModel();

		panel.RefreshReasons([new Error("validation error")]);
		panel.ReportWarning("operation warning");
		panel.ClearOperation();

		panel.Entries.Should().ContainSingle().Which.IsError.Should().BeTrue();
	}

	[AvaloniaFact]
	public void ReportSuccess_DoesNotSetHasEntries()
	{
		var panel = new MessagePanelViewModel();

		panel.ReportSuccess("saved");

		panel.HasEntries.Should().BeFalse();
	}

	[AvaloniaFact]
	public void ReportError_SetsHasEntries()
	{
		var panel = new MessagePanelViewModel();

		panel.ReportError("op");

		panel.HasEntries.Should().BeTrue();
	}

	[AvaloniaFact]
	public void ReportWarning_SetsHasEntries()
	{
		var panel = new MessagePanelViewModel();

		panel.ReportWarning("op");

		panel.HasEntries.Should().BeTrue();
	}

	[AvaloniaFact]
	public void ReportWarnings_TypedWarning_SurfacesWarningInOperationSlot()
	{
		var panel = new MessagePanelViewModel();
		var result = Result.Ok().WithSuccess(new RowCountMismatchWarning("recipe.csv", 5, 3));

		panel.ReportWarnings(result);

		panel.Entries.Should().ContainSingle().Which.Severity.Should().Be(MessageSeverity.Warning);
		panel.WarningCount.Should().Be(0);
	}

	[AvaloniaFact]
	public void ReportWarnings_ResultWithoutWarnings_ReportsNothing()
	{
		var panel = new MessagePanelViewModel();

		panel.ReportWarnings(Result.Ok());

		panel.Entries.Should().BeEmpty();
	}

	[AvaloniaFact]
	public void Show_OpensTheHiddenLog()
	{
		using var panel = new MessagePanelViewModel();
		panel.RefreshReasons([new Error("e")]);
		panel.IsVisible = false;

		panel.ShowCommand.Execute().Subscribe();

		panel.ShowPanel.Should().BeTrue();
	}

	[AvaloniaFact]
	public void SecondShow_KeepsTheLogOpen()
	{
		using var panel = new MessagePanelViewModel();
		panel.RefreshReasons([new Error("e")]);

		panel.ShowCommand.Execute().Subscribe();
		panel.ShowCommand.Execute().Subscribe();

		panel.ShowPanel.Should().BeTrue();
	}

	[AvaloniaFact]
	public void Show_WithAnEmptyLog_ShowsNoPanel()
	{
		using var panel = new MessagePanelViewModel();

		panel.ShowCommand.Execute().Subscribe();

		panel.ShowPanel.Should().BeFalse();
	}
}
