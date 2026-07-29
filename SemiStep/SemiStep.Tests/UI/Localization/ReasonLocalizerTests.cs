using System;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using FluentResults;

using SemiStep.Core.Plc.Sync.Ownership;
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
	private static readonly DateTimeOffset _sampleAcquiredUtc = new(2026, 7, 28, 14, 30, 0, TimeSpan.Zero);

	private static OwnedByAnotherInstanceError OwnedByAnother()
	{
		return new OwnedByAnotherInstanceError(new OwnerInfo(123, "MACHINE", "alice", _sampleAcquiredUtc));
	}

	private static FormulaComputationFailedError FormulaFailed()
	{
		return new FormulaComputationFailedError("temp", "min > max");
	}

	[Fact]
	public void Localize_OwnedByAnotherInstance_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(OwnedByAnother())
				.Should().Be("Синхронизация ПЛК занята другим экземпляром (пользователь alice, с 14:30 UTC).");
		}
	}

	[Fact]
	public void Localize_OwnedByAnotherInstance_UnderEnglishCulture_MatchesOriginalMessage()
	{
		var error = OwnedByAnother();

		using (ResourcesCultureScope.Use("en"))
		{
			ReasonLocalizer.Localize(error).Should().Be(error.Message);
		}
	}

	[Fact]
	public void Localize_FormulaComputationFailed_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(FormulaFailed())
				.Should().Be("Вычисление формулы для цели «temp» не выполнено: min > max");
		}
	}

	[Fact]
	public void Localize_FormulaComputationFailed_UnderEnglishCulture_MatchesOriginalMessage()
	{
		var error = FormulaFailed();

		using (ResourcesCultureScope.Use("en"))
		{
			ReasonLocalizer.Localize(error).Should().Be(error.Message);
		}
	}

	[Theory]
	[InlineData("ru")]
	[InlineData("en")]
	public void Localize_FreeTextError_IsUnchanged(string culture)
	{
		var error = new Error("nothing typed here");

		using (ResourcesCultureScope.Use(culture))
		{
			ReasonLocalizer.Localize(error).Should().Be("nothing typed here");
		}
	}

	[Theory]
	[InlineData("ru")]
	[InlineData("en")]
	public void Localize_FreeTextWarning_IsUnchanged(string culture)
	{
		var warning = new Warning("unmatched EndFor at step 3");

		using (ResourcesCultureScope.Use(culture))
		{
			ReasonLocalizer.Localize(warning).Should().Be("unmatched EndFor at step 3");
		}
	}

	[Fact]
	public void Localize_NestedStepColumnDecorators_UnderRussianCulture_ComposesLocalizedPositions()
	{
		var error = new AtStepError(3, new AtColumnError("gas", new Error("bad")));

		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(error).Should().Be("Шаг 3: Столбец «gas»: bad");
		}
	}

	[Fact]
	public void Localize_NestedStepColumnDecorators_UnderEnglishCulture_ComposesEnglishPositions()
	{
		var error = new AtStepError(3, new AtColumnError("gas", new Error("bad")));

		using (ResourcesCultureScope.Use("en"))
		{
			ReasonLocalizer.Localize(error).Should().Be("Step 3: Column 'gas': bad");
		}
	}

	[Fact]
	public void Localize_ColumnDecorator_UnderRussianCulture_ComposesLocalizedPosition()
	{
		var error = new AtColumnError("gas", new Error("bad"));

		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(error).Should().Be("Столбец «gas»: bad");
		}
	}

	[Fact]
	public void Localize_TypedErrorNestedUnderUntypedWrapper_UnderRussianCulture_StillLocalizes()
	{
		var wrapped = new Error("wrap").CausedBy(FormulaFailed());

		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(wrapped)
				.Should().Be("Вычисление формулы для цели «temp» не выполнено: min > max");
		}
	}

	[AvaloniaFact]
	public void ReportFailure_TypedError_UnderRussianCulture_SurfacesRussianSentenceInPanel()
	{
		using var panel = new MessagePanelViewModel();
		var result = Result.Fail(OwnedByAnother());

		using (ResourcesCultureScope.Use("ru"))
		{
			panel.ReportFailure(result);
		}

		var entry = panel.Entries.Should().ContainSingle(item => item.IsError).Subject;
		entry.Message.Should().Be("Синхронизация ПЛК занята другим экземпляром (пользователь alice, с 14:30 UTC).");
	}

	[AvaloniaFact]
	public void RefreshReasons_TypedError_UnderRussianCulture_SurfacesRussianSentenceInPanel()
	{
		using var panel = new MessagePanelViewModel();

		using (ResourcesCultureScope.Use("ru"))
		{
			panel.RefreshReasons([FormulaFailed()]);
		}

		var entry = panel.Entries.Should().ContainSingle(item => item.IsError).Subject;
		entry.Message.Should().Be("Вычисление формулы для цели «temp» не выполнено: min > max");
	}
}
