using System;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Reactive.Disposables;

using Avalonia.Media;

using FluentResults;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.UI.Localization;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Thin parent for the in-app style editor. It exposes each group of <see cref="GridStyleOptions"/> as a
/// per-group <see cref="ReactiveObject"/> draft (initializer-seeded from a mutable copy of the loaded
/// record, never the DI singleton); the AXAML binds grouped <c>Group.Leaf</c> paths against those drafts.
/// <see cref="CanSave"/> gates on the VM-side numeric range checks alone; a typed <see cref="StyleColor"/>
/// cannot hold an invalid color, so there is nothing else to validate. <see cref="BuildRecord"/> constructs
/// a new record positionally from the ten drafts' <c>Build()</c> calls, carrying the one unsurfaced field
/// <see cref="GridStyleOptions.Orientation"/> straight from <c>_source.Orientation</c>.
/// </summary>
public sealed class GridStyleEditorViewModel : ReactiveObject
{
	public const int MinFontSize = 6;
	public const int MaxFontSize = 72;
	public const int MinRowHeight = 12;
	public const int MaxRowHeight = 400;
	public const int MinPadding = 0;
	public const int MaxPadding = 100;
	public const int MinSpacing = 0;
	public const int MaxSpacing = 100;
	public const int MinPanelMaxHeight = 20;
	public const int MaxPanelMaxHeight = 2000;

	private readonly IGridStyleEditorFacade _gridStyleEditorFacade;
	private readonly string _configDir;
	private readonly ILogger<GridStyleEditorViewModel> _logger;

	private GridStyleOptions _source;
	private CompositeDisposable _draftSubscriptions = new();

	public GridStyleEditorViewModel(
		IGridStyleEditorFacade gridStyleEditorFacade,
		StartupOptions startupOptions,
		ILogger<GridStyleEditorViewModel> logger)
		: this(gridStyleEditorFacade, startupOptions.ConfigDir, GridStyleOptions.Default, logger)
	{
	}

	public GridStyleEditorViewModel(
		IGridStyleEditorFacade gridStyleEditorFacade,
		string configDir,
		GridStyleOptions source,
		ILogger<GridStyleEditorViewModel> logger)
	{
		_gridStyleEditorFacade = gridStyleEditorFacade;
		_configDir = configDir;
		_source = source;
		_logger = logger;

		SaveCommand = ReactiveCommand.CreateFromTask(
			SaveAsync,
			this.WhenAnyValue(viewModel => viewModel.CanSave));

		// Modal editor: a save fault must surface on the editor's own ErrorMessage, not the
		// shared message panel hidden behind the dialog. Subscription lifetime tracks this
		// transient dialog VM, which is neither IDisposable nor pooled.
		SaveCommand.ThrownExceptions.Subscribe(ReportSaveException);

		ReplaceDrafts(source);
	}

	public ReactiveCommand<Unit, bool> SaveCommand { get; }

	public string? ErrorMessage
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public bool CanSave
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	// Raising change on a whole-draft swap re-evaluates every bound leaf path, so a successful
	// re-seed refreshes the editor.
	public GridStyleFontsDraft Fonts
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public GridStyleLayoutDraft Layout
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public SelectionColorsDraft Selection
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public ChangedCellColorsDraft ChangedCells
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public DepthPaletteDraft ReadOnlyCells
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public DepthPaletteDraft DisabledCells
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public ExecutionPaletteDraft Execution
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public StatusBarStyleDraft StatusBar
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public ValidationPanelStyleDraft ValidationPanel
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public ChromeColorsDraft Chrome
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	// Picker sources. Always contain the seeded value so the bound string/int is never nulled
	// by a missing SelectedItem/SelectedValue (protects the lossless-seed contract).
	public IReadOnlyList<FontFamilyOption> AvailableFontFamilies
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	} = [];

	public IReadOnlyList<FontWeightOption> AvailableFontWeights
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	} = [];

	public async Task LoadAsync()
	{
		var result = await _gridStyleEditorFacade.Load(_configDir);
		if (result.IsFailed)
		{
			ErrorMessage = string.Join("; ", result.Errors.Select(ReasonLocalizer.Localize));
			LogCausedByExceptions(result, "Grid style load failed");
			return;
		}

		_source = result.Value;
		ErrorMessage = null;
		ReplaceDrafts(result.Value);
	}

	public GridStyleOptions BuildRecord()
	{
		return new GridStyleOptions(
			Fonts: Fonts.Build(),
			Layout: Layout.Build(),
			Selection: Selection.Build(),
			ChangedCells: ChangedCells.Build(),
			ReadOnlyCells: ReadOnlyCells.Build(),
			DisabledCells: DisabledCells.Build(),
			Execution: Execution.Build(),
			StatusBar: StatusBar.Build(),
			ValidationPanel: ValidationPanel.Build(),
			Chrome: Chrome.Build(),
			Orientation: _source.Orientation);
	}

	private async Task<bool> SaveAsync()
	{
		if (!CanSave)
		{
			ErrorMessage = Resources.EditorCannotSave;
			return false;
		}

		var result = await _gridStyleEditorFacade.Save(_configDir, BuildRecord());
		if (result.IsFailed)
		{
			ErrorMessage = string.Join("; ", result.Errors.Select(ReasonLocalizer.Localize));
			LogCausedByExceptions(result, "Grid style save failed");
			return false;
		}

		ErrorMessage = null;
		return true;
	}

	internal void ReportSaveException(Exception exception)
	{
		_logger.LogError(exception, "Style editor save failed");
		ErrorMessage = Resources.SaveFailed;
	}

	private void LogCausedByExceptions(IResultBase result, string message)
	{
		foreach (var exceptional in result.Errors.SelectMany(error => error.Reasons).OfType<ExceptionalError>())
		{
			_logger.LogWarning(exceptional.Exception, "{Message}", message);
		}
	}

	// Swaps the CanSave subscriptions so a stale draft never keeps driving CanSave.
	[MemberNotNull(
		nameof(Fonts),
		nameof(Layout),
		nameof(Selection),
		nameof(ChangedCells),
		nameof(ReadOnlyCells),
		nameof(DisabledCells),
		nameof(Execution),
		nameof(StatusBar),
		nameof(ValidationPanel),
		nameof(Chrome))]
	private void ReplaceDrafts(GridStyleOptions options)
	{
		Fonts = new GridStyleFontsDraft(options.Fonts);
		Layout = new GridStyleLayoutDraft(options.Layout);
		Selection = new SelectionColorsDraft(options.Selection);
		ChangedCells = new ChangedCellColorsDraft(options.ChangedCells);
		ReadOnlyCells = new DepthPaletteDraft(options.ReadOnlyCells);
		DisabledCells = new DepthPaletteDraft(options.DisabledCells);
		Execution = new ExecutionPaletteDraft(options.Execution);
		StatusBar = new StatusBarStyleDraft(options.StatusBar);
		ValidationPanel = new ValidationPanelStyleDraft(options.ValidationPanel);
		Chrome = new ChromeColorsDraft(options.Chrome);

		AvailableFontFamilies = BuildFontFamilies(options.Fonts.FontFamily);
		AvailableFontWeights = BuildFontWeights(options);

		_draftSubscriptions.Dispose();
		_draftSubscriptions = new CompositeDisposable(
			SubscribeCanSave(Fonts),
			SubscribeCanSave(Layout),
			SubscribeCanSave(Selection),
			SubscribeCanSave(ChangedCells),
			SubscribeCanSave(ReadOnlyCells),
			SubscribeCanSave(DisabledCells),
			SubscribeCanSave(Execution),
			SubscribeCanSave(StatusBar),
			SubscribeCanSave(ValidationPanel),
			SubscribeCanSave(Chrome));

		RecomputeCanSave();
	}

	// Takes the base ReactiveObject so the ChangedCellColorsDraft.Changed leaf (which hides the base
	// Changed observable with `new`) resolves to the observable here, not the color property.
	private IDisposable SubscribeCanSave(ReactiveObject draft)
	{
		return draft.Changed.Subscribe(_ => RecomputeCanSave());
	}

	private static IReadOnlyList<FontFamilyOption> BuildFontFamilies(string seededFamily)
	{
		var systemFamilies = FontManager.Current.SystemFonts
			.Select(font => font.Name)
			.Where(name => !string.IsNullOrWhiteSpace(name))
			.Distinct()
			.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
			.ToList();

		var families = new List<FontFamilyOption> { new("", Resources.EditorDefaultFont) };
		if (!string.IsNullOrWhiteSpace(seededFamily) && !systemFamilies.Contains(seededFamily))
		{
			families.Add(new FontFamilyOption(seededFamily, seededFamily));
		}

		families.AddRange(systemFamilies.Select(name => new FontFamilyOption(name, name)));
		return families;
	}

	private static IReadOnlyList<FontWeightOption> BuildFontWeights(GridStyleOptions options)
	{
		var weights = new List<FontWeightOption>
		{
			new(300, "Light"),
			new(400, "Normal"),
			new(500, "Medium"),
			new(600, "SemiBold"),
			new(700, "Bold"),
			new(800, "ExtraBold"),
			new(900, "Black")
		};

		var seededWeights = new[]
		{
			options.Fonts.HeaderFontWeight,
			options.Fonts.CellFontWeight,
			options.StatusBar.Weight,
			options.StatusBar.TimerLabelWeight,
			options.StatusBar.TimerValueWeight
		};

		foreach (var weight in seededWeights.Distinct())
		{
			if (weights.All(option => option.Value != weight))
			{
				weights.Add(new FontWeightOption(weight, weight.ToString()));
			}
		}

		return weights;
	}

	private void RecomputeCanSave()
	{
		CanSave = NumericsInRange();
	}

	private bool NumericsInRange()
	{
		return InRange(Fonts.HeaderFontSize, MinFontSize, MaxFontSize)
			&& InRange(Fonts.CellFontSize, MinFontSize, MaxFontSize)
			&& InRange(Layout.RowHeight, MinRowHeight, MaxRowHeight)
			&& InRange(Layout.CellPaddingLeft, MinPadding, MaxPadding)
			&& InRange(Layout.CellPaddingTop, MinPadding, MaxPadding)
			&& InRange(Layout.CellPaddingRight, MinPadding, MaxPadding)
			&& InRange(Layout.CellPaddingBottom, MinPadding, MaxPadding)
			&& InRange(StatusBar.Padding, MinPadding, MaxPadding)
			&& InRange(StatusBar.ItemSpacing, MinSpacing, MaxSpacing)
			&& InRange(StatusBar.FontSize, MinFontSize, MaxFontSize)
			&& InRange(StatusBar.TimerLabelFontSize, MinFontSize, MaxFontSize)
			&& InRange(StatusBar.TimerValueFontSize, MinFontSize, MaxFontSize)
			&& InRange(ValidationPanel.MaxHeight, MinPanelMaxHeight, MaxPanelMaxHeight);
	}

	private static bool InRange(decimal? value, int min, int max)
	{
		return value is not null && value >= min && value <= max;
	}
}
