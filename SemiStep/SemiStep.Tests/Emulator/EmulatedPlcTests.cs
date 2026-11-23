using System.Buffers.Binary;

using AwesomeAssertions;

using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Core.Plc.S7.Serialization;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;
using SemiStep.Tools.PlcEmulator;

using Xunit;

using static SemiStep.Tests.Helpers.EmulatorTestSupport;

namespace SemiStep.Tests.Emulator;

[Trait("Component", "S7")]
[Trait("Area", "Emulator")]
[Trait("Category", "Integration")]
public sealed class EmulatedPlcTests
{
	[Fact]
	public async Task Start_RiePreset_ServesFiveDataBlocksWithConfiguredSizes()
	{
		var configuration = await ShippedConfigLocator.LoadAsync("RIE");
		var layout = configuration.PlcConfiguration.Layout;
		var stringMaxChars = new RecipeMetadataRegistry(configuration).GetStringMaxLength();
		var stringElementSize =
			new ArrayCodec(layout.IntDb, layout.FloatDb, layout.StringDb, stringMaxChars).WStringElementSize;

		using var plc = StartOrFail(configuration, FreePort());

		var sizes = plc.Blocks.ToDictionary(block => block.DbNumber, block => block.Size);
		sizes.Keys.Should().BeEquivalentTo(
			[
				layout.ManagingDb.DbNumber,
				layout.ExecutionDb.DbNumber,
				layout.IntDb.DbNumber,
				layout.FloatDb.DbNumber,
				layout.StringDb.DbNumber
			]);
		sizes[layout.ManagingDb.DbNumber].Should().Be(layout.ManagingDb.TotalSize);
		sizes[layout.ExecutionDb.DbNumber].Should().Be(layout.ExecutionDb.TotalSize);
		sizes[layout.IntDb.DbNumber].Should().Be(
			layout.IntDb.DataStartOffset + (IntCapacity * ProtocolConstants.IntElementSize));
		sizes[layout.FloatDb.DbNumber].Should().Be(
			layout.FloatDb.DataStartOffset + (FloatCapacity * ProtocolConstants.FloatElementSize));
		sizes[layout.StringDb.DbNumber].Should().Be(layout.StringDb.DataStartOffset + (StringCapacity * stringElementSize));
	}

	[Fact]
	public async Task Start_RiePreset_SeedsProtocolVersionOne()
	{
		var configuration = await ShippedConfigLocator.LoadAsync("RIE");
		var managing = configuration.PlcConfiguration.Layout.ManagingDb;

		using var plc = StartOrFail(configuration, FreePort());

		var version = plc.Read(managing.DbNumber, managing.VersionOffset, sizeof(int));
		BinaryPrimitives.ReadInt32BigEndian(version).Should().Be(1);
	}

	[Fact]
	public async Task Start_PortInUseByAnotherEmulator_Fails()
	{
		var configuration = await ShippedConfigLocator.LoadAsync("RIE");
		var port = FreePort();
		using var first = StartOrFail(configuration, port);

		var second = EmulatedPlc.Start(configuration, OptionsFor(port));

		second.IsFailed.Should().BeTrue();
	}
}
