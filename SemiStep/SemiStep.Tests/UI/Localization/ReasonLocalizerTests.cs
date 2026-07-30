using System;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using FluentResults;

using SemiStep.Core.Plc.Sync.Ownership;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Analysis.Warnings;
using SemiStep.Core.Recipes.Errors;
using SemiStep.Core.Recipes.Formulas.Errors;
using SemiStep.Core.Recipes.Import.Errors;
using SemiStep.Core.Recipes.Import.Warnings;
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
		return new FormulaComputationFailedError("temp", new Error("min > max"));
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
	public void Localize_UnmatchedEndForWarning_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(new UnmatchedEndForWarning(2))
				.Should().Be("Непарный EndFor на шаге 2");
		}
	}

	[Fact]
	public void Localize_UnclosedForLoopWarning_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(new UnclosedForLoopWarning(5))
				.Should().Be("Незакрытый цикл For, начатый на шаге 5");
		}
	}

	[Fact]
	public void Localize_TypedLoopWarnings_UnderEnglishCulture_MatchOriginalMessage()
	{
		Warning[] samples = [new UnmatchedEndForWarning(2), new UnclosedForLoopWarning(5)];

		using (ResourcesCultureScope.Use("en"))
		{
			foreach (var sample in samples)
			{
				ReasonLocalizer.Localize(sample).Should().Be(sample.Message);
			}
		}
	}

	[Fact]
	public void Localize_RowCountMismatchWarning_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(new RowCountMismatchWarning("recipe.csv", 5, 3))
				.Should().Be("Несоответствие количества строк в «recipe.csv»: метаданные указывают 5, фактически 3");
		}
	}

	[Fact]
	public void Localize_RowCountMismatchWarning_UnderEnglishCulture_MatchesOriginalMessage()
	{
		var sample = new RowCountMismatchWarning("recipe.csv", 5, 3);

		using (ResourcesCultureScope.Use("en"))
		{
			ReasonLocalizer.Localize(sample).Should().Be(sample.Message);
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

	[Fact]
	public void Localize_ValueAboveMaximum_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(new ValueAboveMaximumError(500, 100, "temperature"))
				.Should().Be("Значение 500 больше максимума 100 для «temperature»");
		}
	}

	[Fact]
	public void Localize_StringTooLong_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(new StringTooLongError(20, 8, "label"))
				.Should().Be("Длина строки 20 превышает максимум 8 для «label»");
		}
	}

	[Fact]
	public void Localize_ColumnNotFound_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(new ColumnNotFoundError("temp"))
				.Should().Be("Столбец «temp» не найден");
		}
	}

	[Fact]
	public void Localize_ValueNotInGroup_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(new ValueNotInGroupError(7, "valves"))
				.Should().Be("Значение 7 не является допустимым членом группы «valves»");
		}
	}

	[Fact]
	public void Localize_RecipeValueErrors_UnderEnglishCulture_MatchOriginalMessage()
	{
		IError[] samples =
		[
			new ValueAboveMaximumError(500, 100, "temperature"),
			new StringTooLongError(20, 8, "label"),
			new ColumnNotFoundError("temp"),
			new ValueNotInGroupError(7, "valves")
		];

		using (ResourcesCultureScope.Use("en"))
		{
			foreach (var sample in samples)
			{
				ReasonLocalizer.Localize(sample).Should().Be(sample.Message);
			}
		}
	}

	[Fact]
	public void Localize_StepIndexOutOfRange_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(new StepIndexOutOfRangeError(5, 3))
				.Should().Be("Индекс шага 5 вне диапазона для рецепта из 3 шагов");
		}
	}

	[Fact]
	public void Localize_PropertyValueParse_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(new PropertyValueParseError("abc", "integer"))
				.Should().Be("Не удалось разобрать «abc» как integer");
		}
	}

	[Fact]
	public void Localize_IterationCountUnsupportedType_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(new IterationCountUnsupportedTypeError(PropertyType.String, 42))
				.Should().Be("Свойство счётчика итераций имеет неподдерживаемый тип String в шаге 42");
		}
	}

	[Fact]
	public void Localize_RemainingRecipeErrors_UnderEnglishCulture_MatchOriginalMessage()
	{
		IError[] samples =
		[
			new StepIndexOutOfRangeError(5, 3),
			new PropertyValueParseError("abc", "integer"),
			new IterationCountUnsupportedTypeError(PropertyType.String, 42)
		];

		using (ResourcesCultureScope.Use("en"))
		{
			foreach (var sample in samples)
			{
				ReasonLocalizer.Localize(sample).Should().Be(sample.Message);
			}
		}
	}

	[Fact]
	public void Localize_FormulaComputationFailed_WrappingTypedInner_UnderRussianCulture_ComposesRussianDetail()
	{
		var error = new FormulaComputationFailedError("temp", new ValueAboveMaximumError(500, 100, "temperature"));

		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(error)
				.Should().Be("Вычисление формулы для цели «temp» не выполнено: Значение 500 больше максимума 100 для «temperature»");
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

	[Fact]
	public void Localize_CsvRowColumnValueChain_UnderRussianCulture_ComposesRussianDetail()
	{
		var error = new AtRowError(2, new AtColumnError("gas", new ValueAboveMaximumError(5, 4, "amount")));

		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(error)
				.Should().Be("Строка 2: Столбец «gas»: Значение 5 больше максимума 4 для «amount»");
		}
	}

	[Fact]
	public void Localize_CsvHeaderMismatch_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(new CsvHeaderMismatchError("action; step", "action; temp"))
				.Should().Be("Несоответствие заголовка CSV. Ожидалось: [action; step], фактически: [action; temp]");
		}
	}

	[Fact]
	public void Localize_ActionValueNotInteger_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(new ActionValueNotIntegerError("abc"))
				.Should().Be("Не удалось разобрать значение действия «abc» как целое число");
		}
	}

	[Fact]
	public void Localize_ActionColumnNotFound_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(new ActionColumnNotFoundError())
				.Should().Be("Столбец действия не найден");
		}
	}

	[Fact]
	public void Localize_ActionColumnEmpty_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(new ActionColumnEmptyError())
				.Should().Be("Столбец действия пуст");
		}
	}

	[Fact]
	public void Localize_CsvBodyEmpty_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(new CsvBodyEmptyError())
				.Should().Be("Тело CSV пусто");
		}
	}

	[Fact]
	public void Localize_RecipeFileNotFound_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(new RecipeFileNotFoundError("recipe.csv"))
				.Should().Be("Файл рецепта не найден: recipe.csv");
		}
	}

	[Fact]
	public void Localize_RecipeLoadFailed_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(new RecipeLoadFailedError("recipe.csv"))
				.Should().Be("Не удалось загрузить рецепт из «recipe.csv»");
		}
	}

	[Fact]
	public void Localize_RecipeSaveFailed_UnderRussianCulture_RendersRussianSentence()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			ReasonLocalizer.Localize(new RecipeSaveFailedError("recipe.csv"))
				.Should().Be("Не удалось сохранить рецепт в «recipe.csv»");
		}
	}

	[Fact]
	public void Localize_CsvImportErrors_UnderEnglishCulture_MatchOriginalMessage()
	{
		IError[] samples =
		[
			new AtRowError(2, new AtColumnError("gas", new ValueAboveMaximumError(5, 4, "amount"))),
			new CsvHeaderMismatchError("action; step", "action; temp"),
			new ActionValueNotIntegerError("abc"),
			new ActionColumnNotFoundError(),
			new ActionColumnEmptyError(),
			new CsvBodyEmptyError(),
			new RecipeFileNotFoundError("recipe.csv"),
			new RecipeLoadFailedError("recipe.csv"),
			new RecipeSaveFailedError("recipe.csv")
		];

		using (ResourcesCultureScope.Use("en"))
		{
			foreach (var sample in samples)
			{
				ReasonLocalizer.Localize(sample).Should().Be(sample.Message);
			}
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
