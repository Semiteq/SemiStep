using FluentAssertions;

using SemiStep.Core.Recipes.Formulas;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Recipes.Formulas;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Area", "Formulas")]
public sealed class FormulaIdentifierExtractorTests
{
	[Fact]
	public void Extract_SimpleSum_ReturnsBothOperands()
	{
		var result = FormulaIdentifierExtractor.Extract("a + b");

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().BeEquivalentTo(new[] { "a", "b" });
	}

	[Fact]
	public void Extract_NestedExpression_ReturnsAllIdentifiers()
	{
		var result = FormulaIdentifierExtractor.Extract("(x - y) / z * 60");

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().BeEquivalentTo(new[] { "x", "y", "z" });
	}

	[Fact]
	public void Extract_LiteralsOnly_ReturnsEmptySet()
	{
		var result = FormulaIdentifierExtractor.Extract("3.14 * 2");

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().BeEmpty();
	}

	[Fact]
	public void Extract_SqrtBuiltIn_ExcludesFunctionName()
	{
		var result = FormulaIdentifierExtractor.Extract("Sqrt(a*a + b*b)");

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().BeEquivalentTo(new[] { "a", "b" });
	}

	[Fact]
	public void Extract_PowAbsBuiltIns_ExcludeFunctionNames()
	{
		var result = FormulaIdentifierExtractor.Extract("Pow(x, 2) + Abs(y)");

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().BeEquivalentTo(new[] { "x", "y" });
	}

	[Fact]
	public void Extract_UnparseableExpression_ReturnsFailure()
	{
		var result = FormulaIdentifierExtractor.Extract("a +");

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public void Extract_CaseInsensitive_TreatsSameIdentifier()
	{
		var result = FormulaIdentifierExtractor.Extract("A + a");

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().HaveCount(1);
		result.Value.Contains("a").Should().BeTrue();
		result.Value.Contains("A").Should().BeTrue();
	}

	[Fact]
	public void Extract_EmptySource_ReturnsFailure()
	{
		var result = FormulaIdentifierExtractor.Extract("");

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public void ParseAndCompile_ValidExpression_ReturnsLogicalExpression()
	{
		var result = FormulaIdentifierExtractor.ParseAndCompile("(a - b) / c * 60");

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().NotBeNull();
	}

	[Fact]
	public void ParseAndCompile_InvalidExpression_ReturnsFailure()
	{
		var result = FormulaIdentifierExtractor.ParseAndCompile("a +");

		result.IsFailed.Should().BeTrue();
	}
}
