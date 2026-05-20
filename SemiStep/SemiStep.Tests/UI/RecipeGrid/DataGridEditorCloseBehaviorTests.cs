using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class DataGridEditorCloseBehaviorTests
{
	[AvaloniaFact]
	public void Trigger_Emission_ReachesBehaviorSubscription()
	{
		// Verify the behavior actually subscribes to the trigger and routes emissions
		// through its pipeline. (DataGrid.CommitEdit is non-virtual so cannot be spied
		// on directly; instrument the trigger observable instead.)
		var dataGrid = new DataGrid();

		var emissionsReachingBehavior = 0;
		var sourceSubject = new Subject<Unit>();
		var instrumented = sourceSubject
			.Do(_ => emissionsReachingBehavior++);

		DataGridEditorCloseBehavior.SetTrigger(dataGrid, instrumented);

		sourceSubject.OnNext(Unit.Default);
		sourceSubject.OnNext(Unit.Default);

		emissionsReachingBehavior.Should().Be(2);
	}

	[AvaloniaFact]
	public void Trigger_Replacement_DisposesPreviousSubscription()
	{
		var dataGrid = new DataGrid();

		var subscriptionCount = 0;
		var triggerA = Observable.Create<Unit>(observer =>
		{
			subscriptionCount++;
			return Disposable.Create(() => subscriptionCount--);
		});

		var triggerB = new Subject<Unit>();

		DataGridEditorCloseBehavior.SetTrigger(dataGrid, triggerA);
		subscriptionCount.Should().Be(1);

		DataGridEditorCloseBehavior.SetTrigger(dataGrid, triggerB);
		subscriptionCount.Should().Be(0);
	}

	[AvaloniaFact]
	public void Trigger_SetToNull_DisposesPreviousSubscription()
	{
		var dataGrid = new DataGrid();

		var subscriptionCount = 0;
		var trigger = Observable.Create<Unit>(observer =>
		{
			subscriptionCount++;
			return Disposable.Create(() => subscriptionCount--);
		});

		DataGridEditorCloseBehavior.SetTrigger(dataGrid, trigger);
		subscriptionCount.Should().Be(1);

		DataGridEditorCloseBehavior.SetTrigger(dataGrid, null);

		subscriptionCount.Should().Be(0);
		DataGridEditorCloseBehavior.GetTrigger(dataGrid).Should().BeNull();
	}
}
