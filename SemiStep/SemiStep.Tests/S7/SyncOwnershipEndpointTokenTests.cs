using System.IO;

using FluentAssertions;

using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.Sync.Ownership;

using Xunit;

namespace SemiStep.Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "Ownership")]
[Trait("Category", "Unit")]
public sealed class SyncOwnershipEndpointTokenTests
{
	[Fact]
	public void For_SameEndpoint_ProducesIdenticalTokenAcrossCalls()
	{
		var endpoint = new PlcConnectionSettings("192.168.0.150", 102, 0, 2);

		var first = SyncOwnershipEndpointToken.For(endpoint);
		var second = SyncOwnershipEndpointToken.For(endpoint);

		first.Should().Be(second);
	}

	[Fact]
	public void For_EqualEndpointInstances_ProduceIdenticalTokens()
	{
		var endpoint = new PlcConnectionSettings("10.0.0.1", 102, 1, 3);
		var equalEndpoint = new PlcConnectionSettings("10.0.0.1", 102, 1, 3);

		SyncOwnershipEndpointToken.For(endpoint)
			.Should().Be(SyncOwnershipEndpointToken.For(equalEndpoint));
	}

	[Fact]
	public void For_DifferentEndpoints_ProduceDifferentTokens()
	{
		var first = SyncOwnershipEndpointToken.For(new PlcConnectionSettings("10.0.0.1", 102, 0, 2));
		var second = SyncOwnershipEndpointToken.For(new PlcConnectionSettings("10.0.0.2", 102, 0, 2));

		first.Should().NotBe(second);
	}

	[Theory]
	[InlineData("192.168.0.150", 102, 0, 2)]
	[InlineData("10.0.0.1", 102, 1, 3)]
	[InlineData("255.255.255.255", 65535, 7, 31)]
	public void For_RepresentativeEndpoints_ContainsNoPathInvalidCharacters(
		string ipAddress, int port, int rack, int slot)
	{
		var token = SyncOwnershipEndpointToken.For(new PlcConnectionSettings(ipAddress, port, rack, slot));

		token.Should().NotBeNullOrEmpty();
		token.IndexOfAny(Path.GetInvalidFileNameChars()).Should().Be(-1);
	}

	[Fact]
	public void For_EndpointWithPathHostileCharacters_SanitizesThemAway()
	{
		var endpoint = new PlcConnectionSettings("a/b\\c:d", 102, 0, 2);

		var token = SyncOwnershipEndpointToken.For(endpoint);

		token.IndexOfAny(Path.GetInvalidFileNameChars()).Should().Be(-1);
		token.Should().NotContain("/");
		token.Should().NotContain("\\");
		token.Should().NotContain(":");
	}
}
