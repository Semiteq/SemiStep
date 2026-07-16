using System;

using Avalonia.Controls;

namespace SemiStep.UI.RecipeGrid.Transposed;

/// <summary>
/// A <see cref="ListBox"/> whose container-lifecycle overrides recycle the item
/// <c>ContentPresenter</c>'s child subtree in place instead of rebuilding it on every scroll recycle.
///
/// Version-coupled to Avalonia 12.0.5: the overrides depend on the framework's <c>SetIfUnset</c> /
/// <c>ValueStore.IsSet</c> semantics and the exact <c>ClearContainerForItemOverride</c> clearing set.
/// Re-verify them on a framework upgrade. See <c>Docs/architecture/recipe-grid-surface.md</c> for the
/// full mechanism.
/// </summary>
public sealed class TransposedStepListBox : ListBox
{
	// StyleKey drives implicit ControlTheme resolution and bare-type selector matching, defaulting to the
	// concrete runtime type. Left at the subtype, the box would get no Semi.Avalonia ListBox template (no
	// ScrollViewer, no realized containers) and the type-scoped selectors in TransposedGridStyles.axaml
	// (ListBox.transposed-grid and its descendants) would stop matching. Pointing StyleKey back at ListBox
	// restores both.
	protected override Type StyleKeyOverride => typeof(ListBox);

	// Skip base deliberately: the base clear nulls Content and ContentTemplate, which destroys the
	// recyclable ContentPresenter child (see the type remarks). Only IsSelected must be reset here, because
	// ContainerForItemPreparedOverride writes a SET IsSelected back into the selection model, so a container
	// reused from a formerly-selected column onto an unselected column must come back deselected (no
	// selection bleed). The clear is safe unguarded because the panel unmaps the container from its realized
	// set before clearing (SelectingItemsControl.ContainerSelectionChanged then sees index -1 and no-ops),
	// and TransposedColumnsPanel.OnItemsReset is ordered to preserve that unmap-before-clear invariant.
	//
	// Dormant constraint: ItemsControl.RefreshContainers (fired on an ItemTemplate / DisplayMemberBinding
	// change) relies on this clear resetting ContentTemplate. ItemTemplate is static for this ListBox, so
	// skipping it is a no-op today; a future ItemTemplate swap would silently keep the old template and must
	// revisit this override.
	protected override void ClearContainerForItemOverride(Control container)
	{
		container.ClearValue(ListBoxItem.IsSelectedProperty);
	}

	// Re-point Content explicitly before base: the base PrepareContainerForItemOverride sets Content via
	// SetIfUnset, and ValueStore.IsSet returns true for a recycled container that still carries its prior
	// column (set through SetCurrentValue on the previous prepare), so base would skip the set and leave
	// stale content. The guard keys off ContentProperty (the property actually re-pointed), NOT
	// ContentTemplateProperty: IsSet is also true for style/theme-supplied values, so a template-keyed guard
	// could misfire on a fresh container that a style equipped with a ContentTemplate. The guard only avoids
	// a redundant set; a misfire is idempotent (a fresh container sets Content to the same item base is about
	// to set), so it is not a correctness gate.
	protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
	{
		if (container is ContentControl contentControl && contentControl.IsSet(ContentControl.ContentProperty))
		{
			contentControl.SetCurrentValue(ContentControl.ContentProperty, item);
		}

		base.PrepareContainerForItemOverride(container, item, index);
	}
}
