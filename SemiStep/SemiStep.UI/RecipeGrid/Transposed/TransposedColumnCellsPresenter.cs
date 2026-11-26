using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace SemiStep.UI.RecipeGrid.Transposed;

internal sealed class TransposedColumnCellsPresenter : StackPanel
{
	/// <summary>The right and bottom grid line of every cell; the step column width reserves it.</summary>
	internal const double GridLineWidth = 1;

	// Cell background reads this directly (Source=this) instead of a RelativeSource ancestor lookup
	// that fails while the presenter is pooled/detached.
	public static readonly DirectProperty<TransposedColumnCellsPresenter, bool> IsColumnSelectedProperty =
		AvaloniaProperty.RegisterDirect<TransposedColumnCellsPresenter, bool>(
			nameof(IsColumnSelected),
			presenter => presenter.IsColumnSelected,
			(presenter, value) => presenter.IsColumnSelected = value);

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

	public bool IsColumnSelected
	{
		get;
		set => SetAndRaise(IsColumnSelectedProperty, ref field, value);
	}

	public void BindColumn(StepColumnViewModel column)
	{
		EnsureSlotsBuilt(column);
		DataContext = column;
	}

	// Walks the slot subtree directly (not via focus) so it commits even while the presenter is detached
	// during surface-swap teardown, where a focus-driven commit would not fire.
	public void CommitActiveEditor()
	{
		foreach (var slot in this.GetVisualDescendants().OfType<LazyCellPresenter>())
		{
			slot.CommitEdit();
		}
	}

	// Backstop for the in-place DataContext swap that fires on every container reuse from idle (the normal
	// scroll-recycle path): Avalonia raises this top-down and stops at the slot Borders (their DataContext
	// is locally bound), so it fires while the editor still shows the old cell, before any slot rebinds.
	protected override void OnDataContextBeginUpdate()
	{
		CommitActiveEditor();
		base.OnDataContextBeginUpdate();
	}

	// The cell background MultiBinding resolves its brushes through the attached slot Border. A pooled
	// presenter's legs settle while it is still detached, so the converter runs once with an unreachable
	// resource host and yields no brush. Re-announcing the selection leg on attach re-runs the converter
	// now that the Border can reach the palette resources (the old RelativeSource-ancestor leg did this
	// implicitly by re-emitting once its ListBoxItem ancestor was found). The old value is fabricated as
	// the negation to force the notification (a truthful old==new would be coalesced away); this is safe
	// because the sole consumer is the background MultiBinding, which reads only the new value.
	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		RaisePropertyChanged(IsColumnSelectedProperty, !IsColumnSelected, IsColumnSelected);
	}

	private void EnsureSlotsBuilt(StepColumnViewModel column)
	{
		if (_slotsBuilt)
		{
			return;
		}

		// Slot count is descriptor-driven and constant across columns; derive it from the descriptors.
		var cells = column.Cells;
		for (var slotIndex = 0; slotIndex < _descriptors.Count; slotIndex++)
		{
			var slot = BuildCellSlot(_cellFactory.CreateEditor(cells[slotIndex]), slotIndex);
			slot.Bind(DataContextProperty, new Binding(nameof(StepColumnViewModel.Cells))
			{
				Converter = CellSlotConverter.Instance,
				ConverterParameter = slotIndex,
			});

			Children.Add(slot);
		}

		_slotsBuilt = true;
	}

	private Border BuildCellSlot(Control editor, int slotIndex)
	{
		var border = new Border
		{
			Height = _cellHeight,
			BorderThickness = new Thickness(0, 0, GridLineWidth, GridLineWidth),
			Child = editor,
		};

		border.Classes.Add("transposed-cell");

		var isReadOnlyParameter = _descriptors[slotIndex].IsReadOnlyParameter;
		if (isReadOnlyParameter)
		{
			border.Classes.Add("read-only-cell");
		}

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
			Converter = CellBackgroundConverter.Instance,
			ConverterParameter = isReadOnlyParameter,
			Bindings =
			{
				new Binding { RelativeSource = new RelativeSource(RelativeSourceMode.Self) },
				new Binding($"{nameof(ParameterCellViewModel.Row)}.{nameof(RecipeRowViewModel.ForDepth)}"),
				new Binding($"{nameof(ParameterCellViewModel.Row)}.{nameof(RecipeRowViewModel.IsPastStep)}"),
				new Binding($"{nameof(ParameterCellViewModel.Row)}.{nameof(RecipeRowViewModel.IsCurrentStep)}"),
				new Binding(nameof(ParameterCellViewModel.IsApplicable))
				{
					Converter = TransposedCellTemplateFactory.NegateConverter,
				},
				new Binding(nameof(ParameterCellViewModel.IsChanged)),
				new Binding(nameof(IsColumnSelected)) { Source = this },
			},
		});

		return border;
	}
}
