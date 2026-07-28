using System.IO;
using System.Reactive;
using System.Reactive.Linq;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;

using SemiStep.Tests.Config.Helpers;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.MainWindow;
using SemiStep.UI.StyleEditor;

using Xunit;

namespace SemiStep.Tests.UI.MainWindow;

[Trait("Component", "UI")]
[Trait("Area", "StyleEditor")]
[Trait("Category", "Integration")]
public sealed class MainWindowStyleEditorInteractionTests : IAsyncLifetime
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
	public async Task OpenStyleEditorCommand_InvokesInteractionWithLoadedViewModel()
	{
		using var configDir = CopyShippedConfig("MBE");
		var facade = new GridStyleEditorFacade();
		var loaded = (await facade.Load(configDir.Path)).Value;

		var viewModel = _fixture.CreateMainWindowViewModel(
			styleEditorFactory: () => new GridStyleEditorViewModel(
				facade,
				configDir.Path,
				loaded,
				NullLogger<GridStyleEditorViewModel>.Instance));

		try
		{
			GridStyleEditorViewModel? captured = null;
			viewModel.ShowStyleEditorInteraction.RegisterHandler(context =>
			{
				captured = context.Input;
				context.SetOutput(Unit.Default);
			});

			await viewModel.OpenStyleEditorCommand.Execute();

			captured.Should().NotBeNull("the command must route the loaded editor VM through the interaction");
			captured!.ErrorMessage.Should().BeNull("a loadable config means LoadAsync succeeds before the dialog opens");
		}
		finally
		{
			viewModel.Dispose();
		}
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
