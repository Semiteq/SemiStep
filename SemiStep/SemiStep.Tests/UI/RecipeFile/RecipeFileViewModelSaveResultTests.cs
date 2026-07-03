using System.Reactive.Linq;

using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using FluentAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.MessageService;
using SemiStep.UI.RecipeFile;

using Xunit;

namespace SemiStep.Tests.UI.RecipeFile;

[Trait("Component", "UI")]
[Trait("Area", "RecipeFile")]
[Trait("Category", "Integration")]
public sealed class RecipeFileViewModelSaveResultTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private readonly string _tempFilePath =
		Path.Combine(Path.GetTempPath(), $"SemiStep.SaveResult.{Guid.NewGuid():N}.csv");
	private RecipeFileViewModel _recipeFile = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_recipeFile = new RecipeFileViewModel(_fixture.Coordinator, _fixture.MessagePanel);
	}

	public async ValueTask DisposeAsync()
	{
		_recipeFile.Dispose();
		await _fixture.DisposeAsync();
		if (File.Exists(_tempFilePath))
		{
			File.Delete(_tempFilePath);
		}
	}

	[AvaloniaFact]
	public async Task SaveRecipe_Success_EmitsTrueAndSetsCurrentFilePath()
	{
		_recipeFile.SaveFileInteraction.RegisterHandler(context => context.SetOutput(_tempFilePath));

		var saved = await _recipeFile.SaveRecipeCommand.Execute();

		saved.Should().BeTrue("a completed coordinator save is a success");
		_recipeFile.CurrentFilePath.Should().Be(_tempFilePath);
		File.Exists(_tempFilePath).Should().BeTrue();
	}

	[AvaloniaFact]
	public async Task SaveRecipe_FailedCoordinatorSave_EmitsFalseAndKeepsCurrentFilePathUnset()
	{
		var driver = new RecipeTestDriver(_fixture.Session);
		driver.AddFor(3).AddWait(1f);
		_fixture.Session.IsValid.Should().BeFalse("the recipe has an unclosed For loop");
		_recipeFile.SaveFileInteraction.RegisterHandler(context => context.SetOutput(_tempFilePath));

		var saved = await _recipeFile.SaveRecipeCommand.Execute();

		saved.Should().BeFalse("the coordinator rejects saving a structurally defective recipe");
		_recipeFile.CurrentFilePath.Should().BeNull("a failed save must not adopt the target path");
		File.Exists(_tempFilePath).Should().BeFalse();
	}

	[AvaloniaFact]
	public async Task SaveRecipe_CancelledPicker_EmitsFalseAndPerformsNoSave()
	{
		_recipeFile.SaveFileInteraction.RegisterHandler(context => context.SetOutput(null));

		var saved = await _recipeFile.SaveRecipeCommand.Execute();

		saved.Should().BeFalse("a cancelled picker means nothing was saved");
		_recipeFile.CurrentFilePath.Should().BeNull();
	}

	[AvaloniaFact]
	public async Task SaveAsRecipe_CancelledPicker_EmitsFalseAndPerformsNoSave()
	{
		_recipeFile.SaveFileInteraction.RegisterHandler(context => context.SetOutput(null));

		var saved = await _recipeFile.SaveAsRecipeCommand.Execute();

		saved.Should().BeFalse("a cancelled picker means nothing was saved");
		_recipeFile.CurrentFilePath.Should().BeNull();
	}

	[AvaloniaFact]
	public async Task SaveRecipe_WithoutCurrentFilePath_RoutesThroughSaveAsPicker()
	{
		var pickerInvoked = false;
		_recipeFile.SaveFileInteraction.RegisterHandler(context =>
		{
			pickerInvoked = true;
			context.SetOutput(_tempFilePath);
		});

		var saved = await _recipeFile.SaveRecipeCommand.Execute();

		pickerInvoked.Should().BeTrue("Save without a current file path must ask for one");
		saved.Should().BeTrue();
		_recipeFile.CurrentFilePath.Should().Be(_tempFilePath);
	}

	[AvaloniaFact]
	public async Task SaveRecipe_WithCurrentFilePath_DoesNotInvokePicker()
	{
		var pickerInvocationCount = 0;
		_recipeFile.SaveFileInteraction.RegisterHandler(context =>
		{
			pickerInvocationCount++;
			context.SetOutput(_tempFilePath);
		});
		await _recipeFile.SaveRecipeCommand.Execute();

		var saved = await _recipeFile.SaveRecipeCommand.Execute();

		saved.Should().BeTrue();
		pickerInvocationCount.Should().Be(1, "the second save reuses the established file path");
	}

	[AvaloniaFact]
	public async Task SaveRecipe_WithCurrentFilePath_FailedCoordinatorSave_EmitsFalseWithoutPicker()
	{
		var pickerInvocationCount = 0;
		_recipeFile.SaveFileInteraction.RegisterHandler(context =>
		{
			pickerInvocationCount++;
			context.SetOutput(_tempFilePath);
		});
		(await _recipeFile.SaveRecipeCommand.Execute()).Should().BeTrue("the arrange save establishes the file path");
		var driver = new RecipeTestDriver(_fixture.Session);
		driver.AddFor(3).AddWait(1f);
		_fixture.Session.IsValid.Should().BeFalse("the recipe has an unclosed For loop");

		var saved = await _recipeFile.SaveRecipeCommand.Execute();

		saved.Should().BeFalse("the direct-path save must propagate the coordinator failure");
		pickerInvocationCount.Should().Be(1, "a save with an established file path must not open the picker");
		_recipeFile.CurrentFilePath.Should().Be(_tempFilePath);
	}

	[AvaloniaFact]
	public async Task SaveAsRecipe_Success_EmitsTrueAndSetsCurrentFilePath()
	{
		_recipeFile.SaveFileInteraction.RegisterHandler(context => context.SetOutput(_tempFilePath));

		var saved = await _recipeFile.SaveAsRecipeCommand.Execute();

		saved.Should().BeTrue("a completed coordinator save is a success");
		_recipeFile.CurrentFilePath.Should().Be(_tempFilePath);
		File.Exists(_tempFilePath).Should().BeTrue();
	}

	[AvaloniaFact]
	public async Task SaveAsRecipe_ThrowingPicker_ReportsSaveAsFailed()
	{
		_recipeFile.SaveFileInteraction.RegisterHandler(
			_ => throw new InvalidOperationException("disk detached"));

		var saveAs = async () => await _recipeFile.SaveAsRecipeCommand.Execute();

		await saveAs.Should().ThrowAsync<InvalidOperationException>();
		// The ThrownExceptions report is posted to the dispatcher via ObserveOn.
		Dispatcher.UIThread.RunJobs();
		var errorEntry = _fixture.MessagePanel.Entries
			.Should().ContainSingle(e => e.Severity == MessageSeverity.Error).Subject;
		errorEntry.Message.Should().StartWith("Save As failed:");
		errorEntry.Message.Should().Contain("disk detached");
	}
}
