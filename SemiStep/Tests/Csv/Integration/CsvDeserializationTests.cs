using System.Collections.Immutable;

using Core.Entities;

using Domain.Models;

using FluentAssertions;

using Tests.Csv.Helpers;

using Xunit;

namespace Tests.Csv.Integration;

[Trait("Component", "Csv")]
[Trait("Category", "Integration")]
public sealed class CsvDeserializationTests
{
	[Fact]
	[Trait("Feature", "Deserialization")]
	public async Task Deserialize_RoundTrip_PreservesRecipe()
	{
		var (serializer, _) = await CsvTestHelper.BuildAsync();

		var step = new Step(10, ImmutableDictionary<ColumnId, PropertyValue>.Empty
			.Add(new ColumnId("step_duration"), PropertyValue.FromFloat(5.0f))
			.Add(new ColumnId("comment"), PropertyValue.FromString("test comment")));

		var original = new Recipe(ImmutableList.Create(step));
		var csv = serializer.Serialize(original);
		var result = serializer.Deserialize(csv);

		result.IsSuccess.Should().BeTrue();
		result.Recipe.Should().NotBeNull();
		result.Recipe!.StepCount.Should().Be(1);
		result.Recipe.Steps[0].ActionKey.Should().Be(10);
	}

	[Fact]
	[Trait("Feature", "Deserialization")]
	public async Task Deserialize_InvalidActionId_ReturnsError()
	{
		var (serializer, _) = await CsvTestHelper.BuildAsync();

		var csv = "action;step_duration;task;comment\n99999;10;0;test\n";
		var result = serializer.Deserialize(csv);

		result.HasErrors.Should().BeTrue();
		result.Recipe.Should().BeNull();
	}

	[Fact]
	[Trait("Feature", "Deserialization")]
	public async Task Deserialize_EmptyActionColumn_ReturnsError()
	{
		var (serializer, _) = await CsvTestHelper.BuildAsync();

		var csv = "action;step_duration;task;comment\n;10;0;test\n";
		var result = serializer.Deserialize(csv);

		result.HasErrors.Should().BeTrue();
	}

	[Fact]
	[Trait("Feature", "Deserialization")]
	public async Task Deserialize_HeaderMismatch_ReturnsError()
	{
		var (serializer, _) = await CsvTestHelper.BuildAsync();

		var csv = "wrong_header;bad_column\n10;5\n";
		var result = serializer.Deserialize(csv);

		result.HasErrors.Should().BeTrue();
	}
}
