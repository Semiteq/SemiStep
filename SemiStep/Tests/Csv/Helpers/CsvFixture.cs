using Csv.ClipboardService;
using Csv.FsService;

using Xunit;

namespace Tests.Csv.Helpers;

public sealed class CsvFixture : IAsyncLifetime
{
	internal CsvFileSerializer FileSerializer { get; private set; } = null!;
	internal CsvClipboardSerializer ClipboardSerializer { get; private set; } = null!;

	private IServiceProvider? _services;

	public async Task InitializeAsync()
	{
		var (serializer, clipboardSerializer, services) = await CsvTestHelper.BuildAsync();
		FileSerializer = serializer;
		ClipboardSerializer = clipboardSerializer;
		_services = services;
	}

	public async Task DisposeAsync()
	{
		if (_services is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync();
		}
	}
}
