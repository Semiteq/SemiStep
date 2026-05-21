using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Plc;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "RecipeSessionSync")]
[Trait("Category", "Integration")]
public sealed class RecipeSessionSyncIsValidTests
{
	[Fact]
	public async Task Apply_OnDefectiveRecipe_NotifiesSyncWithIsValidFalse()
	{
		var (services, session, _) = await CoreTestHelper.BuildAsync();
		var syncService = (StubPlcSyncService)services.GetRequiredService<IPlcSyncService>();
		syncService.SetSyncEnabled(true);

		var driver = new RecipeTestDriver(session);
		driver.AddFor(3).AddWait(1f);

		syncService.NotifyRecipeChangedCalls.Should().NotBeEmpty();
		syncService.NotifyRecipeChangedCalls[^1].IsValid.Should()
			.BeFalse("the final state has an unclosed For loop so the PLC must observe isValid=false");
	}
}
