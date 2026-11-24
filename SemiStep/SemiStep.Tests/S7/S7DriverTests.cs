using AwesomeAssertions;

using SemiStep.Core.Plc.S7;

using Xunit;

namespace SemiStep.Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "Driver")]
[Trait("Category", "Unit")]
public sealed class S7DriverTests
{
	[Fact]
	public async Task ReadBytesAsync_WithoutPlcInstance_ThrowsDriverNotConnected()
	{
		await using var driver = new S7Driver();

		var read = () => driver.ReadBytesAsync(1, 0, 1, TestContext.Current.CancellationToken);

		await read.Should().ThrowAsync<DriverNotConnectedException>();
	}

	[Fact]
	public async Task WriteBytesAsync_WithoutPlcInstance_ThrowsDriverNotConnected()
	{
		await using var driver = new S7Driver();
		await driver.DisconnectAsync();

		var write = () => driver.WriteBytesAsync(1, 0, [0], TestContext.Current.CancellationToken);

		await write.Should().ThrowAsync<DriverNotConnectedException>();
	}
}
