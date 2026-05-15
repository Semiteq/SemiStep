using SemiStep.Core.Plc;
using SemiStep.Core.Recipes;

using Xunit;

namespace SemiStep.Tests.Core.Helpers;

public sealed class CoreFixture : IAsyncLifetime
{
	public RecipeSession Session { get; private set; } = null!;
	public PlcLifecycleManager Plc { get; private set; } = null!;

	public IServiceProvider Services => _services!;

	private IServiceProvider? _services;

	public async ValueTask InitializeAsync()
	{
		var (services, session, plc) = await CoreTestHelper.BuildAsync("WithGroups");
		_services = services;
		Session = session;
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
