using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Import;

using Xunit;

namespace SemiStep.Tests.Csv.Helpers;

public sealed class CsvFixture : IAsyncLifetime
{
	internal CsvFileSerializer FileSerializer { get; private set; } = null!;
	internal ClipboardSerializer ClipboardSerializer { get; private set; } = null!;

	private IServiceProvider? _services;

	public async ValueTask InitializeAsync()
	{
		var (fileSerializer, clipboardSerializer, services) = await CsvTestHelper.BuildAsync();
		FileSerializer = fileSerializer;
		ClipboardSerializer = clipboardSerializer;
		_services = services;
	}

	public async ValueTask DisposeAsync()
	{
		if (_services is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync();
		}
	}
}
