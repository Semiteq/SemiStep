using System.Collections.Immutable;

using FluentAssertions;

using FluentResults;

using Shared.Core;

using Tests.Csv.Helpers;

using Xunit;

namespace Tests.Csv.Integration;

[Trait("Component", "Csv")]
[Trait("Category", "Integration")]
[Trait("Area", "Deserialization")]
public sealed class CsvDeserializationTests(CsvFixture fixture) : IClassFixture<CsvFixture>
{
	[Fact]
	public void Deserialize_RoundTrip_PreservesRecipe()
	{
		var step = new Step(10, ImmutableDictionary<ColumnId, PropertyValue>.Empty
			.Add(new ColumnId("step_duration"), PropertyValue.FromFloat(5.0f))
			.Add(new ColumnId("comment"), PropertyValue.FromString("test comment")));

		var original = new Recipe(ImmutableList.Create(step));
		var csv = fixture.FileSerializer.Serialize(original);
		var result = fixture.FileSerializer.Deserialize(csv);

		result.IsSuccess.Should().BeTrue();
		result.Value.StepCount.Should().Be(1);
		result.Value.Steps[0].ActionKey.Should().Be(10);
	}

	[Fact]
	public void Deserialize_InvalidActionId_ReturnsError()
	{
		var csv = "action;step_duration;task;comment\n99999;10;0;test\n";
		var result = fixture.FileSerializer.Deserialize(csv);

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public void Deserialize_EmptyActionColumn_ReturnsError()
	{
		var csv = "action;step_duration;task;comment\n;10;0;test\n";
		var result = fixture.FileSerializer.Deserialize(csv);

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public void Deserialize_HeaderMismatch_ReturnsError()
	{
		var csv = "wrong_header;bad_column\n10;5\n";
		var result = fixture.FileSerializer.Deserialize(csv);

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public void ClipboardRoundTrip_SerializeAndDeserializeWithoutHeaders()
	{
		var step1 = new Step(10, ImmutableDictionary<ColumnId, PropertyValue>.Empty
			.Add(new ColumnId("step_duration"), PropertyValue.FromFloat(5.0f))
			.Add(new ColumnId("comment"), PropertyValue.FromString("first")));

		var step2 = new Step(10, ImmutableDictionary<ColumnId, PropertyValue>.Empty
			.Add(new ColumnId("step_duration"), PropertyValue.FromFloat(15.0f))
			.Add(new ColumnId("comment"), PropertyValue.FromString("second")));

		var steps = new List<Step> { step1, step2 };
		var recipe = new Recipe(steps.ToImmutableList());

		var csv = fixture.ClipboardSerializer.SerializeSteps(recipe);
		csv.Should().Contain("\t");
		csv.Should().NotContain(";");

		var result = fixture.ClipboardSerializer.DeserializeSteps(csv);

		result.IsSuccess.Should().BeTrue();
		result.Value.Steps.Count.Should().Be(2);
		result.Value.Steps[0].ActionKey.Should().Be(10);
		result.Value.Steps[1].ActionKey.Should().Be(10);
	}

	[Fact]
	public void ClipboardDeserialize_EmptyString_ReturnsNull()
	{
		var result = fixture.ClipboardSerializer.DeserializeSteps("");

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public void ClipboardDeserialize_InvalidData_ReturnsNull()
	{
		var result = fixture.ClipboardSerializer.DeserializeSteps("not\tvalid\tcsv\tdata");

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public void ClipboardDeserialize_MalformedQuotedData_ReturnsNull()
	{
		var malformed = "\"1\"\t\"\"\t\"\"\t\"\"\t\"\"\t\"\"\t\"\"\t\"\"\t\"\"\t\"\"\t\"\"\t\"\"";
		var result = fixture.ClipboardSerializer.DeserializeSteps(malformed);

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public void ClipboardDeserialize_FewerColumnsThanExpected_SucceedsWithMissingFieldsTreatedAsEmpty()
	{
		var fewerColumns = "10\t5.0";
		var result = fixture.ClipboardSerializer.DeserializeSteps(fewerColumns);

		result.IsSuccess.Should().BeTrue();
		result.Value.Steps.Should().HaveCount(1);
		result.Value.Steps[0].ActionKey.Should().Be(10);
	}

	[Fact]
	public void ClipboardDeserialize_MoreColumnsThanExpected_ReturnsColumnCountMismatch()
	{
		var tooManyColumns = "10\t5.0\t0\ttest\textra";
		var result = fixture.ClipboardSerializer.DeserializeSteps(tooManyColumns);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainSingle()
			.Which.Message.Should().Contain("Column count mismatch");
	}
}
