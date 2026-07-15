using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.VisualTree;

using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid.Transposed;

// One reusable presenter for a step column's cells. Built ONCE with a fixed slot per ParameterDescriptor
// (cell count and row order are constant across columns), each slot a cell Border whose child is a plain
// editor control - NOT a ContentControl - so it survives the detach/reattach a recycled ListBox container
// forces and only rebinds on a DataContext change instead of being rebuilt. Presenters are pooled across
// containers (see TransposedColumnCellsPool): rebinding a column is a single DataContext assignment that
// re-resolves every slot's Cells[i], turning a viewport jump from N column rebuilds into N rebinds.
internal sealed class TransposedColumnCellsPresenter : StackPanel
{
	private readonly IReadOnlyList<ParameterDescriptor> _descriptors;
	private readonly TransposedCellTemplateFactory _cellFactory;
	private readonly double _cellHeight;
	private bool _slotsBuilt;

	public TransposedColumnCellsPresenter(
		IReadOnlyList<ParameterDescriptor> descriptors,
		TransposedCellTemplateFactory cellFactory,
		double cellHeight)
	{
		_descriptors = descriptors;
		_cellFactory = cellFactory;
		_cellHeight = cellHeight;
		Orientation = Orientation.Vertical;
	}

	// Binds the presenter to a column: builds the slots on first use (from the config-constant
	// descriptor count, touching Cells only for this actually-realized column so the never-realized
	// column's Lazy stays untouched), then rebinds every slot by assigning the column DataContext.
	public void BindColumn(StepColumnViewModel column)
	{
		EnsureSlotsBuilt(column);
		DataContext = column;
	}

	// Commits/closes any editing slot (text or combo) inside this presenter before it is released or
	// rebound, so pending text is never lost to the rebind's OneWay display binding and a combo drops back
	// to its display. Walks the slot subtree directly (not via focus) so it works even while the presenter
	// is already detached from its top level during a recycle.
	public void CommitActiveEditor()
	{
		foreach (var slot in this.GetVisualDescendants().OfType<TransposedLazyCellPresenter>())
		{
			slot.CommitEdit();
		}
	}

	// Backstop for the rare in-place DataContext swap: Avalonia raises this top-down and stops at the
	// slot Borders (their DataContext is locally bound), so it fires while the editor still shows the
	// old cell, before any slot rebinds.
	protected override void OnDataContextBeginUpdate()
	{
		CommitActiveEditor();
		base.OnDataContextBeginUpdate();
	}

	private void EnsureSlotsBuilt(StepColumnViewModel column)
	{
		if (_slotsBuilt)
		{
			return;
		}

		// Slot count is descriptor-driven and constant across columns (one cell per ParameterDescriptor);
		// derive it from the descriptors, not this column's Cells, so the count is orientation-invariant.
		var cells = column.Cells;
		for (var slotIndex = 0; slotIndex < _descriptors.Count; slotIndex++)
		{
			var slot = BuildCellSlot(_cellFactory.CreateEditor(cells[slotIndex]));
			slot.Bind(DataContextProperty, new Binding(nameof(StepColumnViewModel.Cells))
			{
				Converter = CellSlotConverter.Instance,
				ConverterParameter = slotIndex,
			});

			Children.Add(slot);
		}

		_slotsBuilt = true;
	}

	// Rebuilds the cell Border in code (the XAML equivalent lived in the inner ItemsControl item
	// template): the transposed-cell chrome classes, the flattened background-state MultiBinding, and a
	// fixed row height. The editor is the Border's direct child.
	private Border BuildCellSlot(Control editor)
	{
		var border = new Border
		{
			Height = _cellHeight,
			Child = editor,
		};

		border.Classes.Add("transposed-cell");

		var readOnlyParameterPath =
			$"{nameof(ParameterCellViewModel.Descriptor)}.{nameof(ParameterDescriptor.IsReadOnlyParameter)}";

		border.BindClass("read-only-cell", new Binding(readOnlyParameterPath), border);
		border.BindClass(
			"inapplicable",
			new Binding(nameof(ParameterCellViewModel.IsApplicable))
			{
				Converter = TransposedCellTemplateFactory.NegateConverter,
			},
			border);
		border.BindClass("changed", new Binding(nameof(ParameterCellViewModel.IsChanged)), border);

		border.Bind(Border.BackgroundProperty, new MultiBinding
		{
			Converter = TransposedCellBackgroundConverter.Instance,
			Bindings =
			{
				new Binding { RelativeSource = new RelativeSource(RelativeSourceMode.Self) },
				new Binding($"{nameof(ParameterCellViewModel.Row)}.{nameof(RecipeRowViewModel.ForDepth)}"),
				new Binding($"{nameof(ParameterCellViewModel.Row)}.{nameof(RecipeRowViewModel.IsPastStep)}"),
				new Binding(readOnlyParameterPath),
				new Binding(nameof(ParameterCellViewModel.IsApplicable)),
				new Binding(nameof(ParameterCellViewModel.IsChanged)),
				new Binding(nameof(ListBoxItem.IsSelected))
				{
					RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
					{
						AncestorType = typeof(ListBoxItem),
					},
				},
			},
		});

		return border;
	}
}
