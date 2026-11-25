using AwesomeAssertions;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.S7;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "Shutdown")]
[Trait("Category", "Unit")]
public sealed class S7ServiceDisposeTests
{
	[Fact]
	public async Task DisposeAsync_DisconnectsButLeavesTheTransportToItsOwner()
	{
		var (service, driver) = S7ServiceTests.BuildService(S7ServiceTests.BuildConfiguration());
		await service.ConnectAsync(PlcConnectionSettings.Default);

		await service.DisposeAsync();

		driver.IsConnected.Should().BeFalse();
		driver.DisposeCount.Should().Be(0, "the container that created the transport disposes it");
	}

	[Fact]
	public async Task DisposeAsync_CalledAgain_DoesNotRepeatTheTeardown()
	{
		var logs = new RecordingLoggerFactory();
		var (service, _) = S7ServiceTests.BuildService(
			S7ServiceTests.BuildConfiguration(),
			logs.CreateLogger<S7Service>());
		await service.ConnectAsync(PlcConnectionSettings.Default);
		await service.DisposeAsync();
		var entriesAfterFirstDispose = logs.Snapshot<S7Service>().Count;

		await service.DisposeAsync();

		logs.Snapshot<S7Service>().Should().HaveCount(entriesAfterFirstDispose);
	}
}
