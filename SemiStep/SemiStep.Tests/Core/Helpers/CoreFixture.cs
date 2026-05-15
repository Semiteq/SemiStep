using SemiStep.Core.Plc;
using SemiStep.Core.Recipes;

using Xunit;

namespace SemiStep.Tests.Core.Helpers;

public sealed class CoreFixture : IAsyncLifetime
{
	public RecipeWorkspace Workspace { get; private set; } = null!;
	public RecipeEditor Editor { get; private set; } = null!;
	public PlcLifecycleManager Plc { get; private set; } = null!;

	public IServiceProvider Services => _services!;

	private IServiceProvider? _services;

	public async ValueTask InitializeAsync()
	{
		var (services, workspace, editor, plc) = await CoreTestHelper.BuildAsync("WithGroups");
		_services = services;
		Workspace = workspace;
		Editor = editor;
		Plc = plc;
	}

	public async ValueTask DisposeAsync()
	{
		if (_services is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync();
		}
	}
}
