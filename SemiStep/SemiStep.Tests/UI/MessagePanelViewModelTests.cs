using Avalonia.Headless.XUnit;

using FluentAssertions;

using FluentResults;

using SemiStep.Core.Recipes.Import.Warnings;
using SemiStep.Core.Shared;

using SemiStep.Tests.UI.Localization;
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

	[AvaloniaTheory]
	[InlineData(1, "Errors: 1")]
	[InlineData(2, "Errors: 2")]
	public void ErrorCountText_UsesLabelThenCount(int errorCount, string expected)
	{
		using (ResourcesCultureScope.Use("en"))
		{
			var panel = new MessagePanelViewModel();
			var reasons = Enumerable.Range(0, errorCount).Select(IReason (i) => new Error($"msg{i}")).ToList();

			panel.RefreshReasons(reasons);

			panel.ErrorCountText.Should().Be(expected);
		}
	}

	[AvaloniaTheory]
	[InlineData(1, 1, "Errors: 1, Warnings: 1")]
	[InlineData(2, 0, "Errors: 2")]
	[InlineData(0, 1, "Warnings: 1")]
	public void StatusErrorSummary_CombinesErrorsAndWarnings(int errorCount, int warningCount, string expected)
	{
		using (ResourcesCultureScope.Use("en"))
		{
			var panel = new MessagePanelViewModel();
			var reasons = new List<IReason>();
			reasons.AddRange(Enumerable.Range(0, errorCount).Select(IReason (i) => new Error($"e{i}")));
			reasons.AddRange(Enumerable.Range(0, warningCount).Select(IReason (i) => new Warning($"w{i}")));

			panel.RefreshReasons(reasons);

			panel.StatusErrorSummary.Should().Be(expected);
		}
	}

	[AvaloniaFact]
	public void ShowPanel_True_WhenHasEntriesAndVisible()
	{
		var panel = new MessagePanelViewModel();
		panel.IsVisible = false;

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

	[AvaloniaTheory]
	[InlineData(true)]
	[InlineData(false)]
	public void RefreshReasons_AddsEntries_PreservingSeverity(bool isError)
	{
		var panel = new MessagePanelViewModel();
		List<IReason> reasons = isError ? [new Error("some error")] : [new Warning("some warning")];

		panel.RefreshReasons(reasons);

		panel.Entries.Should().ContainSingle(e => isError ? e.IsError : e.IsWarning);
	}

	[AvaloniaFact]
	public void RefreshReasons_RemovesOldEntries_BeforeAddingNew()
	{
		var panel = new MessagePanelViewModel();

		panel.RefreshReasons([new Error("old error")]);
		panel.RefreshReasons([]);

		panel.Entries.Should().BeEmpty();
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
	public void RefreshReasons_PreservesOperationEntry()
	{
		var panel = new MessagePanelViewModel();

		panel.ReportError("operation failed");
		panel.RefreshReasons([new Error("validation error")]);

		panel.Entries.Should().HaveCount(2);
		panel.Entries[0].Message.Should().Be("operation failed");
		panel.Entries.Should().Contain(e => e.Message == "validation error");
	}

	[AvaloniaFact]
	public void ReportOperation_ShowsAsRow_LatestOnly()
	{
		var panel = new MessagePanelViewModel();

		panel.ReportError("first operation");
		panel.ReportWarning("second operation");

		panel.Entries.Should().ContainSingle();
		panel.Entries[0].Message.Should().Be("second operation");
		panel.Entries[0].IsWarning.Should().BeTrue();
	}

	[AvaloniaFact]
	public void ReportSuccess_MapsToInfoSeverity()
	{
		var panel = new MessagePanelViewModel();

		panel.ReportSuccess("saved");

		panel.Entries.Should().ContainSingle();
		panel.Entries[0].Severity.Should().Be(MessageSeverity.Info);
	}

	[AvaloniaTheory]
	[InlineData(MessageSeverity.Warning)]
	[InlineData(MessageSeverity.Error)]
	public void ReportOperation_MapsSeverity(MessageSeverity severity)
	{
		var panel = new MessagePanelViewModel();

		if (severity == MessageSeverity.Warning)
		{
			panel.ReportWarning("careful");
		}
		else
		{
			panel.ReportError("failed");
		}

		panel.Entries.Should().ContainSingle();
		panel.Entries[0].Severity.Should().Be(severity);
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
		panel.ReportError("operation error");
		panel.ClearOperation();

		panel.Entries.Should().ContainSingle();
		panel.Entries[0].Message.Should().Be("validation error");
	}

	[AvaloniaFact]
	public void ReportSuccess_DoesNotSetHasEntries()
	{
		var panel = new MessagePanelViewModel();

		panel.ReportSuccess("saved");

		panel.HasEntries.Should().BeFalse();
	}

	[AvaloniaTheory]
	[InlineData(MessageSeverity.Error)]
	[InlineData(MessageSeverity.Warning)]
	public void OperationErrorOrWarning_SetsHasEntries(MessageSeverity severity)
	{
		var panel = new MessagePanelViewModel();

		if (severity == MessageSeverity.Error)
		{
			panel.ReportError("op");
		}
		else
		{
			panel.ReportWarning("op");
		}

		panel.HasEntries.Should().BeTrue();
	}

	[AvaloniaFact]
	public void ReportWarnings_TypedWarning_UnderRussianCulture_SurfacesRussianInOperationSlot()
	{
		var panel = new MessagePanelViewModel();
		var result = Result.Ok().WithSuccess(new RowCountMismatchWarning("recipe.csv", 5, 3));

		using (ResourcesCultureScope.Use("ru"))
		{
			panel.ReportWarnings(result);
		}

		var entry = panel.Entries.Should().ContainSingle().Subject;
		entry.Severity.Should().Be(MessageSeverity.Warning);
		entry.Message.Should().Be("Несоответствие количества строк в «recipe.csv»: метаданные указывают 5, фактически 3");
	}

	[AvaloniaFact]
	public void ReportWarnings_ResultWithoutWarnings_ReportsNothing()
	{
		var panel = new MessagePanelViewModel();

		panel.ReportWarnings(Result.Ok());

		panel.Entries.Should().BeEmpty();
	}
}
