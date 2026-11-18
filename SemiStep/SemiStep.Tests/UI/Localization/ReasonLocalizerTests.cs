using Avalonia.Headless.XUnit;

using AwesomeAssertions;

using FluentResults;

using SemiStep.Core.Recipes.Errors;
using SemiStep.Core.Recipes.Formulas.Errors;
using SemiStep.Core.Shared;
using SemiStep.UI.Localization;
using SemiStep.UI.MessageService;

using Xunit;

namespace SemiStep.Tests.UI.Localization;

[Trait("Component", "UI")]
[Trait("Area", "Localization")]
[Trait("Category", "Unit")]
public sealed class ReasonLocalizerTests
{
	private static FormulaComputationFailedError FormulaFailed()
	{
		return new FormulaComputationFailedError("temp", new Error("min > max"));
	}

	[Theory]
	[InlineData("ru")]
	[InlineData("en")]
	public void Localize_FreeTextError_ReturnsItsMessage(string culture)
	{
		var error = new Error("nothing typed here");

		using (ResourcesCultureScope.Use(culture))
		{
			ReasonLocalizer.Localize(error).Should().Be(error.Message);
		}
	}

	[Theory]
	[InlineData("ru")]
	[InlineData("en")]
	public void Localize_FreeTextWarning_ReturnsItsMessage(string culture)
	{
		var warning = new Warning("unmatched EndFor at step 3");

		using (ResourcesCultureScope.Use(culture))
		{
			ReasonLocalizer.Localize(warning).Should().Be(warning.Message);
		}
	}

	[Fact]
	public void Localize_NestedStepColumnDecorators_UnderRussianCulture_LocalizesEveryLayer()
	{
		var inner = new Error("bad");
		var column = new AtColumnError("gas", inner);
		var step = new AtStepError(3, column);

		using (ResourcesCultureScope.Use("ru"))
		{
			var localizedColumn = ReasonLocalizer.Localize(column);
			var localizedStep = ReasonLocalizer.Localize(step);

			localizedColumn.Should().EndWith(ReasonLocalizer.Localize(inner));
			localizedColumn.Should().Contain("gas");
			localizedColumn.Should().NotBe(column.Message);
			localizedStep.Should().EndWith(localizedColumn);
			localizedStep.Should().Contain("3");
			localizedStep.Should().NotBe(step.Message);
		}
	}

	[Fact]
	public void Localize_NestedStepColumnDecorators_UnderEnglishCulture_ReturnsMessage()
	{
		var error = new AtStepError(3, new AtColumnError("gas", new Error("bad")));

		using (ResourcesCultureScope.Use("en"))
		{
			ReasonLocalizer.Localize(error).Should().Be(error.Message);
		}
	}

	[Fact]
	public void Localize_FormulaComputationFailed_WrappingTypedInner_EndsWithLocalizedInner()
	{
		var inner = new ValueAboveMaximumError(500, 100, "temperature");
		var error = new FormulaComputationFailedError("temp", inner);

		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(error).Should().EndWith(ReasonLocalizer.Localize(inner));
		}
	}

	[Fact]
	public void Localize_TypedErrorNestedUnderUntypedWrapper_LocalizesLikeTheTypedError()
	{
		var typed = FormulaFailed();
		var wrapped = new Error("wrap").CausedBy(typed);

		using (ResourcesCultureScope.Use("ru"))
		{
			var localized = ReasonLocalizer.Localize(wrapped);

			localized.Should().Be(ReasonLocalizer.Localize(typed));
			localized.Should().NotBe(wrapped.Message);
		}
	}

	[AvaloniaFact]
	public void RefreshReasons_TypedError_UnderRussianCulture_SurfacesLocalizedText()
	{
		using var panel = new MessagePanelViewModel();
		var error = FormulaFailed();

		using (ResourcesCultureScope.Use("ru"))
		{
			panel.RefreshReasons([error]);

			var entry = panel.Entries.Should().ContainSingle(item => item.IsError).Subject;
			entry.Message.Should().Be(ReasonLocalizer.Localize(error));
			entry.Message.Should().NotBe(error.Message);
		}
	}
}
