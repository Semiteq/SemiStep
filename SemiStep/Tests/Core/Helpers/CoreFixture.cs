using Domain.Facade;

using Tests.Helpers;

using Xunit;

namespace Tests.Core.Helpers;

public sealed class CoreFixture : IAsyncLifetime
{
	public DomainFacade Facade { get; private set; } = null!;

	public IServiceProvider Services => _services!;

	private IServiceProvider? _services;

	public async Task InitializeAsync()
	{
		var (services, facade) = await CoreTestHelper.BuildAsync("WithGroups");
		_services = services;
		Facade = facade;
	}

	public async Task DisposeAsync()
	{
		if (_services is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync();
		}
	}
}
