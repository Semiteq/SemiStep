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
	public void For_DifferentEndpoints_ProduceDifferentTokens()
	{
		var first = SyncOwnershipEndpointToken.For(new PlcConnectionSettings("10.0.0.1", 102, 0, 2));
		var second = SyncOwnershipEndpointToken.For(new PlcConnectionSettings("10.0.0.2", 102, 0, 2));

		first.Should().NotBe(second);
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
