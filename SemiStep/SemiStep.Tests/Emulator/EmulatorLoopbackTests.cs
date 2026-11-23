using System.Collections.Concurrent;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using S7.Net;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.S7;
using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Core.Plc.S7.Serialization;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Plc.Sync;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;
using SemiStep.Tools.PlcEmulator;

using Xunit;

using static SemiStep.Tests.Helpers.EmulatorTestSupport;

namespace SemiStep.Tests.Emulator;

[Trait("Component", "S7")]
[Trait("Area", "Emulator")]
[Trait("Category", "Integration")]
public sealed class EmulatorLoopbackTests
{
	private static readonly TimeSpan _waitLimit = TimeSpan.FromSeconds(10);

	[Fact]
	public async Task WriteRecipe_ThroughRealS7Stack_ReadsBackEqualAndTheRunIsVisibleOverS7()
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		var configuration = await ShippedConfigLocator.LoadAsync("RIE");
		using var plc = StartOrFail(configuration, FreePort());
		var events = new ConcurrentQueue<EmulatorEvent>();
		var engine = new RunEngine(plc, new CommittedRecipeLoader(plc, configuration));
		var loop = new EmulatorLoop(plc, engine);
		loop.EventRaised += events.Enqueue;
		using var loopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var looping = loop.RunAsync(TimeProvider.System, loopCancellation.Token);

		try
		{
			await using var client = await ConnectedClient.CreateAsync(configuration, plc.Port);
			var executor = client.Provider.GetRequiredService<PlcTransactionExecutor>();
			var recipe = RieRecipeBuilder.BuildThreeStepRecipe();

			var version = await client.Provider.GetRequiredService<IS7Reader>().ReadProtocolVersionAsync(cancellationToken);
			var written = await executor.WriteRecipeWithRetryAsync(recipe, cancellationToken);
			var readBack = await executor.ReadRecipeFromPlcAsync(cancellationToken);
			await TestHelpers.WaitUntilAsync(
				() => events.Contains(new RecipeCommitted(3)),
				_waitLimit,
				TestHelpers.DefaultPollInterval,
				cancellationToken);
			var run = engine.Run();
			var execution = await ReadExecutionOverS7Async(client, configuration, cancellationToken);

			version.Value.Should().Be(ProtocolConstants.ProtocolVersion);
			written.IsSuccess.Should().BeTrue();
			readBack.Value.Should().Be(recipe);
			run.IsSuccess.Should().BeTrue();
			execution.RecipeActive.Should().BeTrue();
		}
		finally
		{
			await loopCancellation.CancelAsync();
			await looping;
		}
	}

	[Fact]
	public async Task WriteRecipe_TwiceThroughRealS7Stack_ReportsEachCommitOnce()
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		var configuration = await ShippedConfigLocator.LoadAsync("RIE");
		using var plc = StartOrFail(configuration, FreePort());
		var events = new ConcurrentQueue<EmulatorEvent>();
		var loop = new EmulatorLoop(plc, new RunEngine(plc, new CommittedRecipeLoader(plc, configuration)));
		loop.EventRaised += events.Enqueue;
		using var loopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var looping = loop.RunAsync(TimeProvider.System, loopCancellation.Token);

		try
		{
			await using var client = await ConnectedClient.CreateAsync(configuration, plc.Port);
			var executor = client.Provider.GetRequiredService<PlcTransactionExecutor>();
			var recipe = RieRecipeBuilder.BuildThreeStepRecipe();

			await executor.WriteRecipeWithRetryAsync(recipe, cancellationToken);
			await WaitForCommitsAsync(events, 1, cancellationToken);
			await executor.WriteRecipeWithRetryAsync(recipe, cancellationToken);
			await WaitForCommitsAsync(events, 2, cancellationToken);
		}
		finally
		{
			await loopCancellation.CancelAsync();
			await looping;
		}

		loop.Tick();
		events.OfType<RecipeCommitted>().Should().Equal(new RecipeCommitted(3), new RecipeCommitted(3));
	}

	[Fact]
	public async Task WriteBytes_PastEmulatedBlockSize_FailsAndOnlyTheRefusedRequestIsReported()
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		var configuration = await ShippedConfigLocator.LoadAsync("RIE");
		using var plc = StartOrFail(configuration, FreePort());
		await using var client = await ConnectedClient.CreateAsync(configuration, plc.Port);
		var transport = client.Provider.GetRequiredService<IS7Transport>();
		var intDb = configuration.PlcConfiguration.Layout.IntDb;
		var blockSize = plc.Blocks.Single(block => block.DbNumber == intDb.DbNumber).Size;
		var before = plc.Read(intDb.DbNumber, 0, blockSize);
		var events = new ConcurrentQueue<EmulatorEvent>();
		var engine = new RunEngine(plc, new CommittedRecipeLoader(plc, configuration));
		var loop = new EmulatorLoop(plc, engine);
		loop.EventRaised += events.Enqueue;

		await transport.ReadBytesAsync(intDb.DbNumber, 0, blockSize, cancellationToken);
		var write = () => transport.WriteBytesAsync(intDb.DbNumber, 0, new byte[blockSize + 1], cancellationToken);

		await write.Should().ThrowAsync<PlcException>();
		plc.Read(intDb.DbNumber, 0, blockSize).Should().Equal(before);
		await TestHelpers.WaitUntilAsync(
			() =>
			{
				loop.Tick();
				return events.OfType<RequestRefused>().Any();
			},
			_waitLimit,
			TestHelpers.DefaultPollInterval,
			cancellationToken);
		loop.Tick();
		events.OfType<RequestRefused>().Should().ContainSingle();
	}

	[Fact]
	public async Task Tick_ClientConnectsAndDisconnects_ReportsEachOnce()
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		var configuration = await ShippedConfigLocator.LoadAsync("RIE");
		using var plc = StartOrFail(configuration, FreePort());
		var events = new ConcurrentQueue<EmulatorEvent>();
		var loop = new EmulatorLoop(plc, new RunEngine(plc, new CommittedRecipeLoader(plc, configuration)));
		loop.EventRaised += events.Enqueue;

		var client = await ConnectedClient.CreateAsync(configuration, plc.Port);
		await WaitForEventAsync<ClientConnected>(loop, events, cancellationToken);
		await client.DisposeAsync();
		await WaitForEventAsync<ClientDisconnected>(loop, events, cancellationToken);
		loop.Tick();

		events.OfType<ClientConnected>().Should().ContainSingle();
		events.OfType<ClientDisconnected>().Should().ContainSingle();
	}

	private static Task WaitForEventAsync<TEvent>(
		EmulatorLoop loop,
		ConcurrentQueue<EmulatorEvent> events,
		CancellationToken cancellationToken)
		where TEvent : EmulatorEvent
	{
		return TestHelpers.WaitUntilAsync(
			() =>
			{
				loop.Tick();
				return events.OfType<TEvent>().Any();
			},
			_waitLimit,
			TestHelpers.DefaultPollInterval,
			cancellationToken);
	}

	private static Task WaitForCommitsAsync(
		ConcurrentQueue<EmulatorEvent> events,
		int count,
		CancellationToken cancellationToken)
	{
		return TestHelpers.WaitUntilAsync(
			() => events.OfType<RecipeCommitted>().Count() >= count,
			_waitLimit,
			TestHelpers.DefaultPollInterval,
			cancellationToken);
	}

	private static async Task<PlcExecutionInfo> ReadExecutionOverS7Async(
		ConnectedClient client,
		AppConfiguration configuration,
		CancellationToken cancellationToken)
	{
		var execution = configuration.PlcConfiguration.Layout.ExecutionDb;
		var bytes = await client.Provider.GetRequiredService<IS7Transport>()
			.ReadBytesAsync(execution.DbNumber, 0, execution.TotalSize, cancellationToken);
		return new ExecutionStateCodec(execution).Decode(bytes).Value;
	}

	/// <summary>
	/// The app's S7 stack pointed at the emulator. The container would dispose S7Service once per
	/// registered interface and fail on the second call, so the session only disconnects.
	/// </summary>
	private sealed class ConnectedClient : IAsyncDisposable
	{
		private ConnectedClient(ServiceProvider provider)
		{
			Provider = provider;
		}

		public ServiceProvider Provider { get; }

		public static async Task<ConnectedClient> CreateAsync(AppConfiguration configuration, int port)
		{
			var plcConfiguration = configuration.PlcConfiguration with
			{
				Connection = configuration.PlcConfiguration.Connection with { IpAddress = "127.0.0.1", Port = port },
			};
			var appConfiguration = configuration with { PlcConfiguration = plcConfiguration };

			var services = new ServiceCollection();
			services.AddSingleton(appConfiguration);
			services.AddSingleton(new RecipeMetadataRegistry(appConfiguration));
			services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
			services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
			services.AddS7();

			var client = new ConnectedClient(services.BuildServiceProvider());
			using var timeout = new CancellationTokenSource(_waitLimit);
			await client.Provider.GetRequiredService<IS7Connection>()
				.ConnectAsync(plcConfiguration.Connection)
				.WaitAsync(timeout.Token);
			return client;
		}

		public async ValueTask DisposeAsync()
		{
			await Provider.GetRequiredService<IS7Connection>().DisconnectAsync();
		}
	}
}
