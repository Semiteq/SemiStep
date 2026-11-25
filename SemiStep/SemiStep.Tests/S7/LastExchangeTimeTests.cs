using AwesomeAssertions;

using SemiStep.Core.Plc;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "LastExchange")]
[Trait("Category", "Unit")]
public sealed class LastExchangeTimeTests
{
	private const int ShortIntervalMs = 20;
	private const int NeverMs = 100000;

	[Fact]
	public async Task BeforeAnyRead_IsEmpty()
	{
		var (service, _) = S7ServiceTests.BuildService(S7ServiceTests.BuildConfiguration());

		service.LastExchangeTime.Should().BeNull();

		await service.DisposeAsync();
	}

	[Fact]
	public async Task SuccessfulExecutionPoll_MovesItToNow()
	{
		var configuration = S7ServiceTests.BuildConfiguration(keepAliveIntervalMs: NeverMs, pollingIntervalMs: ShortIntervalMs);
		var (service, _) = S7ServiceTests.BuildService(configuration);
		var before = DateTimeOffset.UtcNow;

		await service.ConnectAsync(PlcConnectionSettings.Default);
		await WaitForStampAsync(service);
		var first = service.LastExchangeTime!.Value;
		await TestHelpers.WaitUntilAsync(
			() => service.LastExchangeTime > first,
			cancellationToken: TestContext.Current.CancellationToken);

		first.Should().BeOnOrAfter(before);
		service.LastExchangeTime!.Value.Should().BeOnOrBefore(DateTimeOffset.UtcNow);

		await service.DisposeAsync();
	}

	[Fact]
	public async Task SuccessfulKeepAliveRead_MovesIt()
	{
		var configuration = S7ServiceTests.BuildConfiguration(keepAliveIntervalMs: ShortIntervalMs, pollingIntervalMs: NeverMs);
		var (service, driver) = S7ServiceTests.BuildService(configuration);
		var before = DateTimeOffset.UtcNow;

		await service.ConnectAsync(PlcConnectionSettings.Default);
		await WaitForStampAsync(service);

		service.LastExchangeTime!.Value.Should().BeOnOrAfter(before);
		driver.Calls.Where(call => !call.IsConnect)
			.Should().OnlyContain(call => call.DbNumber == configuration.Layout.ManagingDb.DbNumber,
				"only the keep-alive reads in this setup");

		await service.DisposeAsync();
	}

	[Fact]
	public async Task FailedReadAndLostLink_LeaveItUnchanged()
	{
		var configuration = S7ServiceTests.BuildConfiguration(keepAliveIntervalMs: NeverMs, pollingIntervalMs: ShortIntervalMs);
		var (service, driver) = S7ServiceTests.BuildService(configuration);
		var states = new List<PlcConnectionState>();
		service.StateChanged += state =>
		{
			lock (states)
			{
				states.Add(state);
			}
		};

		await service.ConnectAsync(PlcConnectionSettings.Default);
		await WaitForStampAsync(service);
		driver.SetReadExceptionForDb(configuration.Layout.ExecutionDb.DbNumber, new IOException("read failed"));
		await Task.Delay(ShortIntervalMs * 2, TestContext.Current.CancellationToken);
		var lastGood = service.LastExchangeTime;
		var readsAtFailure = driver.Calls.Count;
		await TestHelpers.WaitUntilAsync(
			() => driver.Calls.Count >= readsAtFailure + 3,
			cancellationToken: TestContext.Current.CancellationToken);

		service.LastExchangeTime.Should().Be(lastGood, "a failed read never stamps");

		driver.SetConnected(false);
		await TestHelpers.WaitUntilAsync(
			() =>
			{
				lock (states)
				{
					return states.Contains(PlcConnectionState.Disconnected);
				}
			},
			cancellationToken: TestContext.Current.CancellationToken);

		service.LastExchangeTime.Should().Be(lastGood, "a lost link keeps the last successful exchange");

		await service.DisposeAsync();
	}

	private static Task WaitForStampAsync(IS7Connection service)
	{
		return TestHelpers.WaitUntilAsync(
			() => service.LastExchangeTime is not null,
			cancellationToken: TestContext.Current.CancellationToken);
	}
}
