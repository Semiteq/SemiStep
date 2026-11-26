using System.Reactive.Linq;

using Avalonia.Headless.XUnit;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeFile;

using Xunit;

namespace SemiStep.Tests.UI.RecipeFile;

[Trait("Component", "UI")]
[Trait("Area", "RecipeFile")]
[Trait("Category", "Integration")]
public sealed class RecipeFileUnsavedChangesTests : IAsyncLifetime
{
	private const int DirtyStepCount = 2;
	private const int OpenedStepCount = 1;

	private readonly UIFixture _fixture = new();
	private readonly string _openPath =
		Path.Combine(Path.GetTempPath(), $"SemiStep.UnsavedOpen.{Guid.NewGuid():N}.csv");
	private readonly string _savePath =
		Path.Combine(Path.GetTempPath(), $"SemiStep.UnsavedSave.{Guid.NewGuid():N}.csv");
	private RecipeFileViewModel _recipeFile = null!;
	private UnsavedChangesChoice _answer;
	private string? _saveAsAnswer;
	private Action? _promptAction;
	private Action? _pickerAction;
	private int _promptCount;
	private int _openPickerCount;
	private int _savePickerCount;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.SeedRecipe(OpenedStepCount);
		await _fixture.CsvService.SaveAsync(_fixture.Coordinator.CurrentRecipe, _openPath);
		_fixture.SeedRecipe(DirtyStepCount);

		_recipeFile = new RecipeFileViewModel(
			_fixture.Coordinator,
			_fixture.MessagePanel,
			NullLogger<RecipeFileViewModel>.Instance);
		_recipeFile.UnsavedChangesInteraction.RegisterHandler(context =>
		{
			_promptCount++;
			_promptAction?.Invoke();
			context.SetOutput(_answer);
		});
		_recipeFile.OpenFileInteraction.RegisterHandler(context =>
		{
			_openPickerCount++;
			_pickerAction?.Invoke();
			context.SetOutput(_openPath);
		});
		_recipeFile.SaveFileInteraction.RegisterHandler(context =>
		{
			_savePickerCount++;
			context.SetOutput(_saveAsAnswer);
		});
	}

	public async ValueTask DisposeAsync()
	{
		_recipeFile.Dispose();
		await _fixture.DisposeAsync();
		File.Delete(_openPath);
		File.Delete(_savePath);
	}

	[AvaloniaFact]
	public async Task New_Dirty_Cancel_KeepsRecipe()
	{
		_answer = UnsavedChangesChoice.Cancel;

		await _recipeFile.NewRecipeCommand.Execute();

		_promptCount.Should().Be(1, "a dirty recipe must not be replaced without asking");
		StepCount.Should().Be(DirtyStepCount, "Cancel keeps the recipe untouched");
		_fixture.Coordinator.IsDirty.Should().BeTrue();
		_savePickerCount.Should().Be(0);
	}

	[AvaloniaFact]
	public async Task New_Dirty_DontSave_StartsEmptyRecipeWithoutSaving()
	{
		_answer = UnsavedChangesChoice.DontSave;

		await _recipeFile.NewRecipeCommand.Execute();

		_promptCount.Should().Be(1);
		StepCount.Should().Be(0, "Don't save goes ahead with the new recipe");
		_savePickerCount.Should().Be(0, "Don't save writes nothing");
	}

	[AvaloniaFact]
	public async Task New_Dirty_SaveWithoutFile_SavesThroughSaveAsThenStartsNew()
	{
		_answer = UnsavedChangesChoice.Save;
		_saveAsAnswer = _savePath;

		await _recipeFile.NewRecipeCommand.Execute();

		_savePickerCount.Should().Be(1, "a recipe with no file is saved through save-as");
		(await SavedStepCountAsync(_savePath)).Should().Be(DirtyStepCount, "the old recipe is written first");
		StepCount.Should().Be(0, "a successful save lets the new recipe start");
		_recipeFile.CurrentFilePath.Should().BeNull("the new recipe has no file yet");
	}

	[AvaloniaFact]
	public async Task New_Dirty_SaveWithFile_SavesToThatFileWithoutPicker()
	{
		_saveAsAnswer = _savePath;
		await _recipeFile.SaveAsRecipeCommand.Execute();
		new RecipeTestDriver(_fixture.Session).AddWait(1f);
		_answer = UnsavedChangesChoice.Save;

		await _recipeFile.NewRecipeCommand.Execute();

		_savePickerCount.Should().Be(1, "only the first save-as opens the picker; the second save reuses the file");
		(await SavedStepCountAsync(_savePath)).Should().Be(DirtyStepCount + 1);
		StepCount.Should().Be(0);
	}

	[AvaloniaFact]
	public async Task New_Dirty_SaveAsCancelled_KeepsRecipe()
	{
		_answer = UnsavedChangesChoice.Save;
		_saveAsAnswer = null;

		await _recipeFile.NewRecipeCommand.Execute();

		_savePickerCount.Should().Be(1);
		StepCount.Should().Be(DirtyStepCount, "a cancelled save-as aborts the new recipe");
		_fixture.Coordinator.IsDirty.Should().BeTrue();
	}

	[AvaloniaFact]
	public async Task New_Dirty_SaveFails_KeepsRecipe()
	{
		new RecipeTestDriver(_fixture.Session).AddFor(3);
		_fixture.Session.IsValid.Should().BeFalse("the recipe has an unclosed For loop");
		var stepCountBefore = StepCount;
		_answer = UnsavedChangesChoice.Save;
		_saveAsAnswer = _savePath;

		await _recipeFile.NewRecipeCommand.Execute();

		File.Exists(_savePath).Should().BeFalse();
		StepCount.Should().Be(stepCountBefore, "a failed save aborts the new recipe");
		_fixture.MessagePanel.Entries.Count(e => e.IsError).Should().Be(1, "the failed save is reported once");
	}

	[AvaloniaFact]
	public async Task New_Dirty_RunStartsDuringPrompt_KeepsRecipe()
	{
		_promptAction = () => _fixture.SetRecipeActive(true);
		_answer = UnsavedChangesChoice.DontSave;

		await _recipeFile.NewRecipeCommand.Execute();

		_promptCount.Should().Be(1);
		StepCount.Should().Be(DirtyStepCount, "a run that started during the prompt blocks the replacement");
		_fixture.Coordinator.IsDirty.Should().BeTrue();
	}

	[AvaloniaFact]
	public async Task New_Clean_StartsEmptyRecipeWithoutPrompt()
	{
		_fixture.Session.MarkSaved();

		await _recipeFile.NewRecipeCommand.Execute();

		_promptCount.Should().Be(0, "a clean recipe has nothing to lose");
		StepCount.Should().Be(0);
	}

	[AvaloniaFact]
	public async Task Open_Dirty_Cancel_KeepsRecipeWithoutPicker()
	{
		_answer = UnsavedChangesChoice.Cancel;

		await _recipeFile.LoadRecipeCommand.Execute();

		_promptCount.Should().Be(1, "a dirty recipe must not be replaced without asking");
		_openPickerCount.Should().Be(0, "Cancel stops before the file picker");
		StepCount.Should().Be(DirtyStepCount);
		_fixture.Coordinator.IsDirty.Should().BeTrue();
	}

	[AvaloniaFact]
	public async Task Open_Dirty_DontSave_OpensPickedFileWithoutSaving()
	{
		_answer = UnsavedChangesChoice.DontSave;

		await _recipeFile.LoadRecipeCommand.Execute();

		_promptCount.Should().Be(1);
		_savePickerCount.Should().Be(0, "Don't save writes nothing");
		_openPickerCount.Should().Be(1);
		StepCount.Should().Be(OpenedStepCount);
		_recipeFile.CurrentFilePath.Should().Be(_openPath);
	}

	[AvaloniaFact]
	public async Task Open_Dirty_SaveWithoutFile_SavesThroughSaveAsThenOpens()
	{
		_answer = UnsavedChangesChoice.Save;
		_saveAsAnswer = _savePath;

		await _recipeFile.LoadRecipeCommand.Execute();

		_savePickerCount.Should().Be(1, "a recipe with no file is saved through save-as");
		(await SavedStepCountAsync(_savePath)).Should().Be(DirtyStepCount, "the old recipe is written first");
		_openPickerCount.Should().Be(1);
		StepCount.Should().Be(OpenedStepCount);
		_recipeFile.CurrentFilePath.Should().Be(_openPath);
	}

	[AvaloniaFact]
	public async Task Open_Dirty_SaveAsCancelled_KeepsRecipeWithoutPicker()
	{
		_answer = UnsavedChangesChoice.Save;
		_saveAsAnswer = null;

		await _recipeFile.LoadRecipeCommand.Execute();

		_savePickerCount.Should().Be(1);
		_openPickerCount.Should().Be(0, "a cancelled save-as aborts the open");
		StepCount.Should().Be(DirtyStepCount);
		_fixture.Coordinator.IsDirty.Should().BeTrue();
	}

	[AvaloniaFact]
	public async Task Open_Dirty_SaveFails_KeepsRecipeWithoutPicker()
	{
		new RecipeTestDriver(_fixture.Session).AddFor(3);
		_fixture.Session.IsValid.Should().BeFalse("the recipe has an unclosed For loop");
		var stepCountBefore = StepCount;
		_answer = UnsavedChangesChoice.Save;
		_saveAsAnswer = _savePath;

		await _recipeFile.LoadRecipeCommand.Execute();

		File.Exists(_savePath).Should().BeFalse();
		_openPickerCount.Should().Be(0, "a failed save aborts the open");
		StepCount.Should().Be(stepCountBefore);
	}

	[AvaloniaFact]
	public async Task Open_Dirty_SaveWithFile_SavesToThatFileWithoutPicker()
	{
		_saveAsAnswer = _savePath;
		await _recipeFile.SaveAsRecipeCommand.Execute();
		new RecipeTestDriver(_fixture.Session).AddWait(1f);
		_answer = UnsavedChangesChoice.Save;

		await _recipeFile.LoadRecipeCommand.Execute();

		_savePickerCount.Should().Be(1, "only the first save-as opens the picker; the second save reuses the file");
		(await SavedStepCountAsync(_savePath)).Should().Be(DirtyStepCount + 1);
		_openPickerCount.Should().Be(1);
		StepCount.Should().Be(OpenedStepCount);
	}

	[AvaloniaFact]
	public async Task Open_Dirty_RunStartsDuringPrompt_KeepsRecipe()
	{
		_promptAction = () => _fixture.SetRecipeActive(true);
		_answer = UnsavedChangesChoice.DontSave;

		await _recipeFile.LoadRecipeCommand.Execute();

		_promptCount.Should().Be(1);
		StepCount.Should().Be(DirtyStepCount, "a run that started during the prompt blocks the open");
		_fixture.Coordinator.IsDirty.Should().BeTrue();
	}

	[AvaloniaFact]
	public async Task Open_Dirty_RunStartsDuringPicker_KeepsRecipe()
	{
		_pickerAction = () => _fixture.SetRecipeActive(true);
		_answer = UnsavedChangesChoice.DontSave;

		await _recipeFile.LoadRecipeCommand.Execute();

		_openPickerCount.Should().Be(1);
		StepCount.Should().Be(DirtyStepCount, "a run that started during the picker blocks the open");
		_recipeFile.CurrentFilePath.Should().BeNull();
	}

	[AvaloniaFact]
	public async Task Open_Clean_OpensPickedFileWithoutPrompt()
	{
		_fixture.Session.MarkSaved();

		await _recipeFile.LoadRecipeCommand.Execute();

		_promptCount.Should().Be(0, "a clean recipe has nothing to lose");
		_openPickerCount.Should().Be(1);
		StepCount.Should().Be(OpenedStepCount);
	}

	private int StepCount => _fixture.Coordinator.CurrentRecipe.StepCount;

	private async Task<int> SavedStepCountAsync(string path)
	{
		File.Exists(path).Should().BeTrue();
		var loaded = await _fixture.CsvService.LoadAsync(path);

		return loaded.Value.StepCount;
	}
}
