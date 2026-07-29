using System.Globalization;
using System.Reactive.Linq;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using Avalonia.Threading;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using ReactiveUI;

using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.Clipboard;
using SemiStep.UI.Localization;
using SemiStep.UI.MessageService;
using SemiStep.UI.RecipeFile;
using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "MessageReporting")]
[Trait("Category", "Unit")]
public sealed class MessagePanelReportingTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private CanonicalRecipeGridSurface _surface = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_surface = _fixture.CreateCanonicalSurface();
		_surface.Initialize();
	}

	public async ValueTask DisposeAsync()
	{
		_surface.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public async Task RecipeFile_Save_ReportsSuccess()
	{
		var recipeFile = new RecipeFileViewModel(
			_fixture.Coordinator,
			_fixture.MessagePanel,
			NullLogger<RecipeFileViewModel>.Instance);
		var filePath = Path.Combine(Path.GetTempPath(), $"semistep-save-{Guid.NewGuid():N}.csv");
		recipeFile.SaveFileInteraction.RegisterHandler(context => context.SetOutput(filePath));

		try
		{
			await recipeFile.SaveAsRecipeCommand.Execute();

			var operationEntry = _fixture.MessagePanel.Entries.First();
			operationEntry.Severity.Should().Be(MessageSeverity.Info);
			operationEntry.Message.Should().Be(
				string.Format(CultureInfo.CurrentCulture, Resources.SavedFormat, Path.GetFileName(filePath)));
		}
		finally
		{
			recipeFile.Dispose();
			File.Delete(filePath);
		}
	}

	[AvaloniaFact]
	public async Task RecipeFile_Load_ReportsSuccess()
	{
		var recipeFile = new RecipeFileViewModel(
			_fixture.Coordinator,
			_fixture.MessagePanel,
			NullLogger<RecipeFileViewModel>.Instance);
		var filePath = Path.Combine(Path.GetTempPath(), $"semistep-load-{Guid.NewGuid():N}.csv");
		recipeFile.SaveFileInteraction.RegisterHandler(context => context.SetOutput(filePath));
		recipeFile.OpenFileInteraction.RegisterHandler(context => context.SetOutput(filePath));

		try
		{
			await recipeFile.SaveAsRecipeCommand.Execute();

			await recipeFile.LoadRecipeCommand.Execute();

			var operationEntry = _fixture.MessagePanel.Entries.First();
			operationEntry.Severity.Should().Be(MessageSeverity.Info);
			operationEntry.Message.Should().Be(
				string.Format(CultureInfo.CurrentCulture, Resources.LoadedFormat, Path.GetFileName(filePath)));
		}
		finally
		{
			recipeFile.Dispose();
			File.Delete(filePath);
		}
	}

	[AvaloniaFact]
	public async Task RecipeFile_LoadMissingFile_ReportsError()
	{
		var recipeFile = new RecipeFileViewModel(
			_fixture.Coordinator,
			_fixture.MessagePanel,
			NullLogger<RecipeFileViewModel>.Instance);
		var missingPath = Path.Combine(Path.GetTempPath(), $"semistep-missing-{Guid.NewGuid():N}.csv");
		recipeFile.OpenFileInteraction.RegisterHandler(context => context.SetOutput(missingPath));

		try
		{
			await recipeFile.LoadRecipeCommand.Execute();

			var operationEntry = _fixture.MessagePanel.Entries.First();
			operationEntry.Severity.Should().Be(MessageSeverity.Error);
		}
		finally
		{
			recipeFile.Dispose();
		}
	}

	[AvaloniaFact]
	public void RecipeGrid_InvalidCellEdit_ReportsError()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_surface.RecipeRows[0].SetPropertyValue(RecipeTestDriver.StepDurationColumn, "not_a_valid_number");

		var operationEntry = _fixture.MessagePanel.Entries.Should().ContainSingle(entry => entry.IsError).Subject;
		operationEntry.Message.Should().StartWith(
			string.Format(CultureInfo.CurrentCulture, Resources.StepFormat, 1) + ":");
		_fixture.MessagePanel.ErrorCount.Should().Be(0,
			"a rejected edit is an operation outcome surfaced transiently, not a structural defect counted in the panel");
	}

	[AvaloniaFact]
	public void RecipeGrid_ChangeToUnknownAction_ReportsError()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_surface.RecipeRows[0].SetPropertyValue("action", "999999");

		var operationEntry = _fixture.MessagePanel.Entries.Should().ContainSingle(entry => entry.IsError).Subject;
		operationEntry.Message.Should().StartWith(
			string.Format(CultureInfo.CurrentCulture, Resources.StepActionChangeFailedFormat, 1));
		_fixture.MessagePanel.ErrorCount.Should().Be(0,
			"a rejected action change is an operation outcome surfaced transiently, not a structural defect counted in the panel");
	}

	[AvaloniaFact]
	public async Task Clipboard_PasteInvalidContent_ReportsError()
	{
		var clipboardSerializer = new ClipboardSerializer(_fixture.RecipeMetadataRegistry);
		var importedRecipeValidator = new ImportedRecipeValidator(_fixture.RecipeMetadataRegistry);
		var clipboardViewModel = new ClipboardViewModel(
			_fixture.Coordinator,
			_surface,
			clipboardSerializer,
			importedRecipeValidator,
			_fixture.MessagePanel,
			NullLogger<ClipboardViewModel>.Instance);

		var window = new Window();
		window.Show();
		Dispatcher.UIThread.RunJobs();

		var clipboard = window.Clipboard!;
		await clipboard.SetTextAsync("this is not valid recipe csv");
		clipboardViewModel.SetClipboard(clipboard);

		try
		{
			await clipboardViewModel.PasteStepCommand.Execute();

			var operationEntry = _fixture.MessagePanel.Entries.First();
			operationEntry.Severity.Should().Be(MessageSeverity.Error);
			operationEntry.Message.Should().StartWith(Resources.PasteStepFailed + ":");
		}
		finally
		{
			clipboardViewModel.Dispose();
			window.Close();
		}
	}

	[AvaloniaFact]
	public async Task Clipboard_PasteValidContent_LeavesNoOperationError()
	{
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var clipboardSerializer = new ClipboardSerializer(_fixture.RecipeMetadataRegistry);
		var importedRecipeValidator = new ImportedRecipeValidator(_fixture.RecipeMetadataRegistry);
		var clipboardViewModel = new ClipboardViewModel(
			_fixture.Coordinator,
			_surface,
			clipboardSerializer,
			importedRecipeValidator,
			_fixture.MessagePanel,
			NullLogger<ClipboardViewModel>.Instance);

		var window = new Window();
		window.Show();
		Dispatcher.UIThread.RunJobs();

		var clipboard = window.Clipboard!;
		clipboardViewModel.SetClipboard(clipboard);

		try
		{
			_surface.UpdateSelection([0]);
			await clipboardViewModel.CopyStepCommand.Execute();

			await clipboardViewModel.PasteStepCommand.Execute();

			_surface.RecipeRows.Count.Should().Be(2,
				"the valid copied step is pasted, doubling the single-step recipe");
			_fixture.MessagePanel.Entries.Should().NotContain(entry => entry.IsError,
				"a successful paste is a successful mutation that clears the operation slot");
		}
		finally
		{
			clipboardViewModel.Dispose();
			window.Close();
		}
	}
}
