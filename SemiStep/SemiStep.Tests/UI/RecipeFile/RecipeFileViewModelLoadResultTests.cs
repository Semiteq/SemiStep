using System.Globalization;
using System.Reactive.Linq;

using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.Localization;
using SemiStep.UI.MessageService;
using SemiStep.UI.RecipeFile;

using Xunit;

namespace SemiStep.Tests.UI.RecipeFile;

[Trait("Component", "UI")]
[Trait("Area", "RecipeFile")]
[Trait("Category", "Integration")]
public sealed class RecipeFileViewModelLoadResultTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private readonly string _tempFilePath =
		Path.Combine(Path.GetTempPath(), $"SemiStep.LoadResult.{Guid.NewGuid():N}.csv");
	private RecipeFileViewModel _recipeFile = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_recipeFile = new RecipeFileViewModel(
			_fixture.Coordinator,
			_fixture.MessagePanel,
			NullLogger<RecipeFileViewModel>.Instance);
		_recipeFile.OpenFileInteraction.RegisterHandler(context => context.SetOutput(_tempFilePath));
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
	public async Task LoadRecipe_RowCountMismatch_SurfacesWarningEntry()
	{
		var driver = new RecipeTestDriver(_fixture.Session);
		driver.AddWait(5f);
		await _fixture.CsvService.SaveAsync(driver.Recipe, _tempFilePath);
		await PatchRowCountAsync(actualRows: 1, wrongRows: 99);

		await _recipeFile.LoadRecipeCommand.Execute();
		Dispatcher.UIThread.RunJobs();

		var entry = _fixture.MessagePanel.Entries.Should().ContainSingle().Subject;
		entry.Severity.Should().Be(MessageSeverity.Warning,
			"a hand-edited row-count mismatch must surface as a warning, not a plain success");
		entry.Message.Should().Contain("Row count mismatch");
	}

	[AvaloniaFact]
	public async Task LoadRecipe_CleanFile_SurfacesSuccessEntryWithoutWarning()
	{
		var driver = new RecipeTestDriver(_fixture.Session);
		driver.AddWait(5f);
		await _fixture.CsvService.SaveAsync(driver.Recipe, _tempFilePath);

		await _recipeFile.LoadRecipeCommand.Execute();
		Dispatcher.UIThread.RunJobs();

		var entry = _fixture.MessagePanel.Entries.Should().ContainSingle().Subject;
		entry.Severity.Should().Be(MessageSeverity.Info, "a clean load reports a plain success");
		entry.Message.Should().Be(
			string.Format(CultureInfo.InvariantCulture, Resources.LoadedFormat, Path.GetFileName(_tempFilePath)));
	}

	[AvaloniaFact]
	public async Task LoadRecipe_StructurallyWarnedRecipeFromCleanFile_KeepsLoadedSuccessInOperationSlot()
	{
		// A structurally-warned recipe (an unclosed For) is saved DIRECTLY through the CSV service,
		// bypassing the coordinator save gate that rejects invalid recipes. The file itself is clean
		// (correct ROWS header), so the load produces no integrity warning: the operation slot must
		// still show the "Loaded" success, with the structural warning confined to the validation list.
		var driver = new RecipeTestDriver(_fixture.Session);
		driver.AddFor(3);
		_fixture.Session.IsValid.Should().BeFalse("an unclosed For loop is a structural defect");
		await _fixture.CsvService.SaveAsync(driver.Recipe, _tempFilePath);

		await _recipeFile.LoadRecipeCommand.Execute();
		Dispatcher.UIThread.RunJobs();

		var entries = _fixture.MessagePanel.Entries;
		entries[0].Severity.Should().Be(MessageSeverity.Info, "the operation slot holds the load success");
		entries[0].Message.Should().Be(
			string.Format(CultureInfo.InvariantCulture, Resources.LoadedFormat, Path.GetFileName(_tempFilePath)));
		entries.Should().Contain(entry => entry.IsWarning,
			"the unclosed-For structural warning belongs in the validation list");
	}

	private async Task PatchRowCountAsync(int actualRows, int wrongRows)
	{
		var original = await File.ReadAllTextAsync(_tempFilePath);
		var patched = original.Replace(
			$"# ROWS=\"{actualRows}\"", $"# ROWS=\"{wrongRows}\"", StringComparison.Ordinal);
		patched.Should().NotBe(original, "the ROWS header must actually change for the fixture to be valid");
		await File.WriteAllTextAsync(_tempFilePath, patched);
	}
}
