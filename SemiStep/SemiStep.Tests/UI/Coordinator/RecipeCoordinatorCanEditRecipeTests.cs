using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Tests.UI.Helpers;

using Xunit;

namespace SemiStep.Tests.UI.Coordinator;

[Trait("Component", "UI")]
[Trait("Area", "Coordinator")]
[Trait("Category", "Integration")]
public sealed class RecipeCoordinatorCanEditRecipeTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();

	public ValueTask InitializeAsync()
	{
		return _fixture.InitializeAsync();
	}

	public ValueTask DisposeAsync()
	{
		return _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void CanEditRecipe_EmitsTrueOnSubscribe_WhenNoExecution()
	{
		var values = CollectValues(_fixture.Coordinator.CanEditRecipe, out var subscription);
		try
		{
			values.Should().ContainSingle().Which.Should().BeTrue();
		}
		finally
		{
			subscription.Dispose();
		}
	}

	[AvaloniaFact]
	public void CanEditRecipe_FlipsToFalse_WhenRecipeBecomesActive()
	{
		var values = CollectValues(_fixture.Coordinator.CanEditRecipe, out var subscription);
		try
		{
			_fixture.SetRecipeActive(true);

			values.Should().Equal(true, false);
		}
		finally
		{
			subscription.Dispose();
		}
	}

	[AvaloniaFact]
	public void CanEditRecipe_FlipsBackToTrue_WhenRecipeStops()
	{
		var values = CollectValues(_fixture.Coordinator.CanEditRecipe, out var subscription);
		try
		{
			_fixture.SetRecipeActive(true);
			_fixture.SetRecipeActive(false);

			values.Should().Equal(true, false, true);
		}
		finally
		{
			subscription.Dispose();
		}
	}

	[AvaloniaFact]
	public void CanEditRecipe_LateSubscriber_ReceivesCurrentValue()
	{
		_fixture.SetRecipeActive(true);

		var values = CollectValues(_fixture.Coordinator.CanEditRecipe, out var subscription);
		try
		{
			values.Should().ContainSingle().Which.Should().BeFalse();
		}
		finally
		{
			subscription.Dispose();
		}
	}

	[AvaloniaFact]
	public void CanEditRecipe_DistinctUntilChanged_SuppressesDuplicateActiveEmissions()
	{
		var values = CollectValues(_fixture.Coordinator.CanEditRecipe, out var subscription);
		try
		{
			_fixture.SetRecipeActive(true);
			_fixture.SetRecipeActive(true);

			values.Should().Equal(true, false);
		}
		finally
		{
			subscription.Dispose();
		}
	}

	private static List<bool> CollectValues(IObservable<bool> source, out IDisposable subscription)
	{
		var values = new List<bool>();
		subscription = source.Subscribe(values.Add);
		return values;
	}
}
