using System.Buffers.Binary;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.Configuration.Memory;
using SemiStep.Core.Plc.S7;
using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Plc.Sync.Ownership;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.S7.Helpers;

using Xunit;

using static SemiStep.Tests.Helpers.EmulatorTestSupport;

namespace SemiStep.Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "Shutdown")]
[Trait("Category", "Integration")]
public sealed class ShutdownOrderTests
{
	private const int FirstArrayWrite = 1;

	private static readonly TimeSpan _waitLimit = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan _blockProbe = TimeSpan.FromMilliseconds(300);
	private static readonly TimeSpan _pastTheDebounce = TimeSpan.FromMilliseconds(1500);

	private static CancellationToken TestToken => TestContext.Current.CancellationToken;

	[Fact]
	public async Task Teardown_DuringAStartedWrite_CompletesTheWriteThenDisconnectsThenReleasesTheLease()
	{
		var configuration = await ShippedConfigLocator.LoadAsync("RIE");
		using var plc = StartOrFail(configuration, FreePort());
		var session = await SyncingSession.StartAsync(configuration, plc.Port);
		var layout = configuration.PlcConfiguration.Layout;
		var hold = new TransportHold(
			layout.IntDb.DbNumber, FirstArrayWrite, TaskCreationOptions.RunContinuationsAsynchronously);
		session.Transport.BeforeWrite = hold.OnCall;
		bool? committedAtDisconnect = null;
		int? leaseReleasesAtDisconnect = null;
		session.Connection.StateChanged += state =>
		{
			if (state == PlcConnectionState.Disconnected)
			{
				committedAtDisconnect = HasCommitWrite(session.Transport, layout.ManagingDb);
				leaseReleasesAtDisconnect = session.Lease.DisposeCallCount;
			}
		};

		session.SyncService.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await hold.Reached.WaitAsync(_waitLimit, TestToken);
		var teardown = Task.Run(() => session.Provider.DisposeInShutdownOrderAsync().AsTask(), TestToken);
		await Task.Delay(_blockProbe, TestToken);

		teardown.IsCompleted.Should().BeFalse("the teardown waits for the started write");
		session.Connection.IsConnected.Should().BeTrue("the disconnect waits for the started write");
		hold.Release();
		await teardown.WaitAsync(_waitLimit, TestToken);

		committedAtDisconnect.Should().BeTrue("the write completes before the disconnect");
		leaseReleasesAtDisconnect.Should().Be(0, "the lease is released after the disconnect");
		session.Lease.DisposeCallCount.Should().Be(1);
	}

	[Fact]
	public async Task Teardown_DuringTheDebounce_WritesNothing()
	{
		var configuration = await ShippedConfigLocator.LoadAsync("RIE");
		using var plc = StartOrFail(configuration, FreePort());
		var session = await SyncingSession.StartAsync(configuration, plc.Port);

		session.SyncService.NotifyRecipeChanged(Recipe.Empty, isValid: true);
		await session.Provider.DisposeInShutdownOrderAsync().AsTask().WaitAsync(_waitLimit, TestToken);
		await Task.Delay(_pastTheDebounce, TestToken);

		session.Transport.WriteLog.Should().BeEmpty("an edit still in the debounce at exit is dropped");
	}

	[Fact]
	public async Task Teardown_WhenTheConnectionDisposeThrows_StillDisposesTheContainer()
	{
		var configuration = await ShippedConfigLocator.LoadAsync("RIE");
		using var plc = StartOrFail(configuration, FreePort());
		var session = await SyncingSession.StartAsync(
			configuration,
			plc.Port,
			services => services.AddSingleton<IS7Connection>(
				sp => new ThrowingOnDisposeConnection(sp.GetRequiredService<S7Service>())));

		var teardown = () => session.Provider.DisposeInShutdownOrderAsync().AsTask();

		await teardown.Should().ThrowAsync<InvalidOperationException>();
		session.Lease.DisposeCallCount.Should().Be(1, "the container still disposes and releases the lease");
	}

	private static bool HasCommitWrite(FakeS7Transport transport, ManagingDbLayout managingDb)
	{
		return transport.WriteLog.Exists(
			write => write.DbNumber == managingDb.DbNumber
				&& write.StartByte == managingDb.CommittedOffset
				&& write.Data[0] == 1);
	}

	/// <summary>
	/// S7Service connects through the real S7Driver to the emulator; only the executor's IS7Transport is faked.
	/// </summary>
	private sealed record SyncingSession(
		ServiceProvider Provider,
		FakeS7Transport Transport,
		IS7Connection Connection,
		IPlcSyncService SyncService,
		StubSyncOwnershipLease Lease)
	{
		public static async Task<SyncingSession> StartAsync(
			AppConfiguration configuration,
			int port,
			Action<IServiceCollection>? overrides = null)
		{
			var plcConfiguration = configuration.PlcConfiguration with
			{
				Connection = configuration.PlcConfiguration.Connection with { IpAddress = "127.0.0.1", Port = port },
			};
			var transport = new FakeS7Transport();
			var managingDb = plcConfiguration.Layout.ManagingDb;
			var version = new byte[sizeof(int)];
			BinaryPrimitives.WriteInt32BigEndian(version, ProtocolConstants.ProtocolVersion);
			transport.SetReadResponse(managingDb.DbNumber, managingDb.VersionOffset, sizeof(int), version);
			var ownership = new StubPlcSyncOwnership();

			var services = new ServiceCollection()
				.AddSingleton(configuration with { PlcConfiguration = plcConfiguration })
				.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
				.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
				.AddRecipe()
				.AddS7()
				.AddSingleton<IS7Transport>(transport)
				.AddSingleton<IPlcSyncOwnership>(ownership);
			overrides?.Invoke(services);
			var provider = services.BuildServiceProvider();

			var lifecycle = provider.GetRequiredService<PlcLifecycleManager>();
			lifecycle.Initialize();
			var enabled = await lifecycle.EnableSync(plcConfiguration).WaitAsync(_waitLimit, TestToken);
			enabled.IsSuccess.Should().BeTrue();

			return new SyncingSession(
				provider,
				transport,
				provider.GetRequiredService<IS7Connection>(),
				provider.GetRequiredService<IPlcSyncService>(),
				ownership.LastLease!);
		}
	}

	private sealed class ThrowingOnDisposeConnection(S7Service service) : IS7Connection
	{
		public bool IsConnected => service.IsConnected;

		public DateTimeOffset? LastExchangeTime => service.LastExchangeTime;

		public event Action<PlcConnectionState>? StateChanged
		{
			add => service.StateChanged += value;
			remove => service.StateChanged -= value;
		}

		public Task ConnectAsync(PlcConnectionSettings settings)
		{
			return service.ConnectAsync(settings);
		}

		public Task DisconnectAsync()
		{
			return service.DisconnectAsync();
		}

		public ValueTask DisposeAsync()
		{
			throw new InvalidOperationException("connection teardown failure");
		}
	}
}
