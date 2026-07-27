using System.Reactive.Linq;

using Avalonia.Headless.XUnit;
using Avalonia.Media;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Tests.Config.Helpers;
using SemiStep.Tests.Helpers;

using SemiStep.UI.StyleEditor;

using Xunit;

namespace SemiStep.Tests.UI.StyleEditor;

[Trait("Component", "UI")]
[Trait("Category", "Unit")]
[Trait("Area", "GridStyleEditor")]
public sealed class GridStyleEditorWindowTests
{
	[AvaloniaFact]
	public async Task Save_AfterEditingColorAndNumeric_PersistsToYaml_AndReloads()
	{
		using var tempDir = CopyShippedConfig("MBE");
		var facade = new GridStyleEditorFacade();
		var configDir = tempDir.Path;

		var loaded = (await facade.Load(configDir)).Value;
		var viewModel = new GridStyleEditorViewModel(
			facade,
			configDir,
			loaded,
			NullLogger<GridStyleEditorViewModel>.Instance);

		viewModel.SelectionBackground = Color.Parse("#123456");
		viewModel.CellFontSize = loaded.CellFontSize + 1;

		viewModel.CanSave.Should().BeTrue();
		var saved = await viewModel.SaveCommand.Execute();

		saved.Should().BeTrue();
		viewModel.ErrorMessage.Should().BeNull();

		var reloaded = (await facade.Load(configDir)).Value;
		reloaded.SelectionBackgroundColor.Should().Be("#123456");
		reloaded.CellFontSize.Should().Be(loaded.CellFontSize + 1);
	}

	[AvaloniaFact]
	public async Task NoEditThenDiscard_LeavesFileByteIdentical()
	{
		using var tempDir = CopyShippedConfig("MBE");
		var facade = new GridStyleEditorFacade();
		var configDir = tempDir.Path;
		var filePath = Path.Combine(configDir, "ui", "grid_style.yaml");

		var before = await File.ReadAllBytesAsync(filePath, TestContext.Current.CancellationToken);

		var loaded = (await facade.Load(configDir)).Value;
		// Construct and discard the VM without invoking Save (mirrors the Cancel path).
		_ = new GridStyleEditorViewModel(
			facade,
			configDir,
			loaded,
			NullLogger<GridStyleEditorViewModel>.Instance);

		var after = await File.ReadAllBytesAsync(filePath, TestContext.Current.CancellationToken);
		after.Should().Equal(before);
	}

	private static TempDirectory CopyShippedConfig(string equipment)
	{
		var source = ShippedConfigLocator.GetConfigDirectory(equipment);
		var tempDir = new TempDirectory();
		var uiDir = Path.Combine(tempDir.Path, "ui");
		Directory.CreateDirectory(uiDir);
		File.Copy(
			Path.Combine(source, "ui", "grid_style.yaml"),
			Path.Combine(uiDir, "grid_style.yaml"),
			overwrite: true);
		return tempDir;
	}
}
