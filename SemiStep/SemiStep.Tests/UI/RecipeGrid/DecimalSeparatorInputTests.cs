using Avalonia.Headless.XUnit;

using AwesomeAssertions;

using SemiStep.Core.Recipes.Errors;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

/// <summary>A number cell editor takes "," or "." as the decimal separator in every culture.</summary>
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Unit")]
public sealed class DecimalSeparatorInputTests : IAsyncLifetime
{
	private const string FloatColumn = "param_01";
	private const string IntColumn = "param_02";

	private readonly UIFixture _fixture = new();
	private CanonicalRecipeGridSurface _surface = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync("WideParams");
		_surface = _fixture.CreateCanonicalSurface();
		_surface.Initialize();
		_fixture.Coordinator.NewRecipe();
		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
	}

	public async ValueTask DisposeAsync()
	{
		_surface.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaTheory]
	[InlineData("ru-RU", "75,5")]
	[InlineData("ru-RU", "75.5")]
	[InlineData("en-US", "75,5")]
	[InlineData("en-US", "75.5")]
	public void FloatCell_EitherDecimalSeparator_StoresTheValue(string culture, string typed)
	{
		using var _ = new CultureScope(culture);

		Commit(FloatColumn, typed);

		Value(FloatColumn).Should().Be(75.5f);
		_fixture.MessagePanel.Entries.Should().NotContain(entry => entry.IsError);
	}

	[AvaloniaTheory]
	[InlineData("ru-RU", "1.234,5")]
	[InlineData("ru-RU", "1,234.5")]
	[InlineData("en-US", "1.234,5")]
	[InlineData("en-US", "1,234.5")]
	public void FloatCell_BothSeparators_IsRejectedWithError(string culture, string typed)
	{
		using var _ = new CultureScope(culture);
		var valueBefore = Value(FloatColumn);

		Commit(FloatColumn, typed);

		Value(FloatColumn).Should().Be(valueBefore);
		_fixture.MessagePanel.Entries.Should().ContainSingle(entry => entry.IsError);
	}

	[AvaloniaTheory]
	[InlineData("ru-RU", "90,5")]
	[InlineData("ru-RU", "90.5")]
	[InlineData("ru-RU", "00:01:30,5")]
	[InlineData("ru-RU", "00:01:30.5")]
	[InlineData("en-US", "90,5")]
	[InlineData("en-US", "90.5")]
	[InlineData("en-US", "00:01:30,5")]
	[InlineData("en-US", "00:01:30.5")]
	public void TimeCell_EitherDecimalSeparator_StoresTheSeconds(string culture, string typed)
	{
		using var _ = new CultureScope(culture);

		Commit(RecipeTestDriver.StepDurationColumn, typed);

		Value(RecipeTestDriver.StepDurationColumn).Should().Be(90.5f);
		_fixture.MessagePanel.Entries.Should().NotContain(entry => entry.IsError);
	}

	[AvaloniaTheory]
	[InlineData("ru-RU")]
	[InlineData("en-US")]
	public void TimeCell_BothSeparators_IsRejectedWithError(string culture)
	{
		using var _ = new CultureScope(culture);
		var valueBefore = Value(RecipeTestDriver.StepDurationColumn);

		Commit(RecipeTestDriver.StepDurationColumn, "1.234,5");

		Value(RecipeTestDriver.StepDurationColumn).Should().Be(valueBefore);
		_fixture.MessagePanel.Entries.Should().ContainSingle(entry => entry.IsError);
	}

	[AvaloniaTheory]
	[InlineData("ru-RU", "00:01:30.5,5")]
	[InlineData("en-US", "00:01:30.5,5")]
	[InlineData("ru-RU", "00:01:30,5.5")]
	public void TimeCell_BothSeparatorsWithColon_IsRejectedWithError(string culture, string typed)
	{
		using var _ = new CultureScope(culture);
		var valueBefore = Value(RecipeTestDriver.StepDurationColumn);

		Commit(RecipeTestDriver.StepDurationColumn, typed);

		Value(RecipeTestDriver.StepDurationColumn).Should().Be(valueBefore);
		_fixture.MessagePanel.Entries.Should().ContainSingle(entry => entry.IsError);
	}

	[AvaloniaTheory]
	[InlineData(FloatColumn, "1,2,3")]
	[InlineData(FloatColumn, "75,5abc")]
	[InlineData(IntColumn, "1,2,3")]
	public void ParseError_QuotesTheTypedText(string columnKey, string typed)
	{
		var row = _surface.RecipeRows[0];
		var formatKind = row.ColumnFormatKinds.GetValueOrDefault(columnKey) ?? TimeFormatHelper.DefaultFormatKind;
		var committed = PropertyTimeEditingConverter.ParseForCommit(typed, formatKind, isTextColumn: false);

		var result = _fixture.Coordinator.UpdateStepProperty(0, columnKey, (string)committed!);

		result.Errors.OfType<PropertyValueParseError>().Should().ContainSingle()
			.Which.RawValue.Should().Be(typed);
	}

	[AvaloniaTheory]
	[InlineData("ru-RU", "75,5")]
	[InlineData("ru-RU", "75.5")]
	[InlineData("en-US", "75,5")]
	[InlineData("en-US", "75.5")]
	public void IntCell_Fraction_IsRejectedWithError(string culture, string typed)
	{
		using var _ = new CultureScope(culture);
		var valueBefore = Value(IntColumn);

		Commit(IntColumn, typed);

		Value(IntColumn).Should().Be(valueBefore);
		_fixture.MessagePanel.Entries.Should().ContainSingle(entry => entry.IsError);
	}

	[AvaloniaTheory]
	[InlineData("ru-RU")]
	[InlineData("en-US")]
	public void TextCell_Comma_IsKeptAsTyped(string culture)
	{
		using var _ = new CultureScope(culture);

		Commit(RecipeTestDriver.CommentColumn, "75,5", isTextColumn: true);

		Value(RecipeTestDriver.CommentColumn).Should().Be("75,5");
	}

	// The commit path both editors share: the editor text through ParseForCommit, then the row write.
	private void Commit(string columnKey, string typed, bool isTextColumn = false)
	{
		var row = _surface.RecipeRows[0];
		var formatKind = row.ColumnFormatKinds.GetValueOrDefault(columnKey) ?? TimeFormatHelper.DefaultFormatKind;
		var committed = PropertyTimeEditingConverter.ParseForCommit(typed, formatKind, isTextColumn);

		committed.Should().BeOfType<string>("the editor hands the text to the model instead of dropping the edit");
		row.SetPropertyValue(columnKey, (string)committed);
	}

	private object? Value(string columnKey)
	{
		return _surface.RecipeRows[0].GetPropertyValue(columnKey);
	}
}
