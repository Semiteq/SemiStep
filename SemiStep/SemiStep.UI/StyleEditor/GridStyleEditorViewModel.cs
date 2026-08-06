using System;
using System.Reactive;
using System.Runtime.CompilerServices;

using Avalonia.Media;

using FluentResults;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.UI.Localization;
using SemiStep.UI.Styles;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="GridStyleOptions"/> for the in-app style editor. Seeded from the
/// loaded record (a separate mutable copy — never the DI singleton), it exposes colors as
/// <see cref="Color"/> and sizes as <see cref="decimal"/>? for two-way binding. <see cref="CanSave"/>
/// gates on the Core color validator AND VM-side numeric range checks (the Core validator is
/// colors-only). The editor surfaces effectively the whole record; <see cref="BuildRecord"/> constructs
/// a new record positionally, carrying the one unsurfaced field <see cref="GridStyleOptions.Orientation"/>
/// straight from <c>_source.Orientation</c>.
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
	private bool _seeding;

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

		Seed(source);
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

	// Numeric draft (decimal? for NumericUpDown).
	public decimal? HeaderFontSize { get => field; set => SetNumber(ref field, value); }
	public decimal? CellFontSize { get => field; set => SetNumber(ref field, value); }
	public decimal? CellPaddingLeft { get => field; set => SetNumber(ref field, value); }
	public decimal? CellPaddingTop { get => field; set => SetNumber(ref field, value); }
	public decimal? CellPaddingRight { get => field; set => SetNumber(ref field, value); }
	public decimal? CellPaddingBottom { get => field; set => SetNumber(ref field, value); }
	public decimal? RowHeight { get => field; set => SetNumber(ref field, value); }
	public decimal? StatusBarPadding { get => field; set => SetNumber(ref field, value); }
	public decimal? StatusBarItemSpacing { get => field; set => SetNumber(ref field, value); }
	public decimal? StatusBarFontSize { get => field; set => SetNumber(ref field, value); }
	public decimal? StatusBarTimerLabelFontSize { get => field; set => SetNumber(ref field, value); }
	public decimal? StatusBarTimerValueFontSize { get => field; set => SetNumber(ref field, value); }
	public decimal? ValidationPanelMaxHeight { get => field; set => SetNumber(ref field, value); }

	// Global font family ("" = theme default). Weight (int 100-900) and italic per role.
	public string? FontFamily { get => field; set => SetValue(ref field, value); }
	public int HeaderFontWeight { get => field; set => SetValue(ref field, value); }
	public bool HeaderItalic { get => field; set => SetValue(ref field, value); }
	public int CellFontWeight { get => field; set => SetValue(ref field, value); }
	public bool CellItalic { get => field; set => SetValue(ref field, value); }
	public int StatusBarFontWeight { get => field; set => SetValue(ref field, value); }
	public bool StatusBarItalic { get => field; set => SetValue(ref field, value); }
	public int StatusBarTimerLabelFontWeight { get => field; set => SetValue(ref field, value); }
	public bool StatusBarTimerLabelItalic { get => field; set => SetValue(ref field, value); }
	public int StatusBarTimerValueFontWeight { get => field; set => SetValue(ref field, value); }
	public bool StatusBarTimerValueItalic { get => field; set => SetValue(ref field, value); }

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

	// Color draft (Color for ColorPicker).
	public Color SelectionBackground { get => field; set => SetColor(ref field, value); }
	public Color SelectionForeground { get => field; set => SetColor(ref field, value); }
	public Color CellChanged { get => field; set => SetColor(ref field, value); }
	public Color CellChangedSelected { get => field; set => SetColor(ref field, value); }
	public Color DisabledCellDepth0 { get => field; set => SetColor(ref field, value); }
	public Color DisabledCellDepth1 { get => field; set => SetColor(ref field, value); }
	public Color DisabledCellDepth2 { get => field; set => SetColor(ref field, value); }
	public Color DisabledCellDepth3 { get => field; set => SetColor(ref field, value); }
	public Color DisabledCellDepth0Past { get => field; set => SetColor(ref field, value); }
	public Color DisabledCellDepth1Past { get => field; set => SetColor(ref field, value); }
	public Color DisabledCellDepth2Past { get => field; set => SetColor(ref field, value); }
	public Color DisabledCellDepth3Past { get => field; set => SetColor(ref field, value); }
	public Color DisabledCellSelected { get => field; set => SetColor(ref field, value); }
	public Color DisabledCellForeground { get => field; set => SetColor(ref field, value); }
	public Color ReadOnlyCellDepth0 { get => field; set => SetColor(ref field, value); }
	public Color ReadOnlyCellDepth1 { get => field; set => SetColor(ref field, value); }
	public Color ReadOnlyCellDepth2 { get => field; set => SetColor(ref field, value); }
	public Color ReadOnlyCellDepth3 { get => field; set => SetColor(ref field, value); }
	public Color ReadOnlyCellDepth0Past { get => field; set => SetColor(ref field, value); }
	public Color ReadOnlyCellDepth1Past { get => field; set => SetColor(ref field, value); }
	public Color ReadOnlyCellDepth2Past { get => field; set => SetColor(ref field, value); }
	public Color ReadOnlyCellDepth3Past { get => field; set => SetColor(ref field, value); }
	public Color ReadOnlyCellSelected { get => field; set => SetColor(ref field, value); }
	public Color ReadOnlyCellForeground { get => field; set => SetColor(ref field, value); }
	public Color GridLine { get => field; set => SetColor(ref field, value); }
	public Color StatusBarBackground { get => field; set => SetColor(ref field, value); }
	public Color StatusBarForeground { get => field; set => SetColor(ref field, value); }
	public Color ValidationPanelBackground { get => field; set => SetColor(ref field, value); }
	public Color ValidationPanelForeground { get => field; set => SetColor(ref field, value); }
	public Color ValidationPanelError { get => field; set => SetColor(ref field, value); }
	public Color ValidationPanelWarning { get => field; set => SetColor(ref field, value); }
	public Color ExecutionDepth0 { get => field; set => SetColor(ref field, value); }
	public Color ExecutionDepth1 { get => field; set => SetColor(ref field, value); }
	public Color ExecutionDepth2 { get => field; set => SetColor(ref field, value); }
	public Color ExecutionDepth3 { get => field; set => SetColor(ref field, value); }
	public Color ExecutionDepth0Past { get => field; set => SetColor(ref field, value); }
	public Color ExecutionDepth1Past { get => field; set => SetColor(ref field, value); }
	public Color ExecutionDepth2Past { get => field; set => SetColor(ref field, value); }
	public Color ExecutionDepth3Past { get => field; set => SetColor(ref field, value); }
	public Color ExecutionCurrentStepMarker { get => field; set => SetColor(ref field, value); }
	public Color Info { get => field; set => SetColor(ref field, value); }
	public Color Connected { get => field; set => SetColor(ref field, value); }
	public Color Disconnected { get => field; set => SetColor(ref field, value); }
	public Color LocalMode { get => field; set => SetColor(ref field, value); }
	public Color Connecting { get => field; set => SetColor(ref field, value); }
	public Color PanelBackground { get => field; set => SetColor(ref field, value); }
	public Color PanelHeaderBackground { get => field; set => SetColor(ref field, value); }
	public Color SubtleBorder { get => field; set => SetColor(ref field, value); }
	public Color Separator { get => field; set => SetColor(ref field, value); }
	public Color SecondaryForeground { get => field; set => SetColor(ref field, value); }
	public Color GridBorder { get => field; set => SetColor(ref field, value); }
	public Color GridBackground { get => field; set => SetColor(ref field, value); }
	public Color HeaderForeground { get => field; set => SetColor(ref field, value); }

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
		Seed(result.Value);
	}

	public GridStyleOptions BuildRecord()
	{
		return new GridStyleOptions(
			Fonts: new GridStyleFonts(
				FontFamily: FontFamily ?? _source.Fonts.FontFamily,
				HeaderFontSize: ToInt(HeaderFontSize, _source.Fonts.HeaderFontSize),
				HeaderFontWeight: HeaderFontWeight,
				HeaderItalic: HeaderItalic,
				CellFontSize: ToInt(CellFontSize, _source.Fonts.CellFontSize),
				CellFontWeight: CellFontWeight,
				CellItalic: CellItalic),
			Layout: new GridStyleLayout(
				CellPaddingLeft: ToDouble(CellPaddingLeft, _source.Layout.CellPaddingLeft),
				CellPaddingTop: ToDouble(CellPaddingTop, _source.Layout.CellPaddingTop),
				CellPaddingRight: ToDouble(CellPaddingRight, _source.Layout.CellPaddingRight),
				CellPaddingBottom: ToDouble(CellPaddingBottom, _source.Layout.CellPaddingBottom),
				RowHeight: ToDouble(RowHeight, _source.Layout.RowHeight)),
			Selection: new SelectionColors(
				Background: SelectionBackground.ToStyleColor(),
				Foreground: SelectionForeground.ToStyleColor()),
			ChangedCells: new ChangedCellColors(
				Changed: CellChanged.ToStyleColor(),
				ChangedSelected: CellChangedSelected.ToStyleColor()),
			ReadOnlyCells: new DepthPalette(
				Depth0: ReadOnlyCellDepth0.ToStyleColor(),
				Depth1: ReadOnlyCellDepth1.ToStyleColor(),
				Depth2: ReadOnlyCellDepth2.ToStyleColor(),
				Depth3: ReadOnlyCellDepth3.ToStyleColor(),
				Depth0Past: ReadOnlyCellDepth0Past.ToStyleColor(),
				Depth1Past: ReadOnlyCellDepth1Past.ToStyleColor(),
				Depth2Past: ReadOnlyCellDepth2Past.ToStyleColor(),
				Depth3Past: ReadOnlyCellDepth3Past.ToStyleColor(),
				Selected: ReadOnlyCellSelected.ToStyleColor(),
				Foreground: ReadOnlyCellForeground.ToStyleColor()),
			DisabledCells: new DepthPalette(
				Depth0: DisabledCellDepth0.ToStyleColor(),
				Depth1: DisabledCellDepth1.ToStyleColor(),
				Depth2: DisabledCellDepth2.ToStyleColor(),
				Depth3: DisabledCellDepth3.ToStyleColor(),
				Depth0Past: DisabledCellDepth0Past.ToStyleColor(),
				Depth1Past: DisabledCellDepth1Past.ToStyleColor(),
				Depth2Past: DisabledCellDepth2Past.ToStyleColor(),
				Depth3Past: DisabledCellDepth3Past.ToStyleColor(),
				Selected: DisabledCellSelected.ToStyleColor(),
				Foreground: DisabledCellForeground.ToStyleColor()),
			Execution: new ExecutionPalette(
				Depth0: ExecutionDepth0.ToStyleColor(),
				Depth1: ExecutionDepth1.ToStyleColor(),
				Depth2: ExecutionDepth2.ToStyleColor(),
				Depth3: ExecutionDepth3.ToStyleColor(),
				Depth0Past: ExecutionDepth0Past.ToStyleColor(),
				Depth1Past: ExecutionDepth1Past.ToStyleColor(),
				Depth2Past: ExecutionDepth2Past.ToStyleColor(),
				Depth3Past: ExecutionDepth3Past.ToStyleColor(),
				CurrentStepMarker: ExecutionCurrentStepMarker.ToStyleColor()),
			StatusBar: new StatusBarStyle(
				Background: StatusBarBackground.ToStyleColor(),
				Foreground: StatusBarForeground.ToStyleColor(),
				Padding: ToDouble(StatusBarPadding, _source.StatusBar.Padding),
				ItemSpacing: ToDouble(StatusBarItemSpacing, _source.StatusBar.ItemSpacing),
				FontSize: ToInt(StatusBarFontSize, _source.StatusBar.FontSize),
				Weight: StatusBarFontWeight,
				Italic: StatusBarItalic,
				TimerLabelFontSize: ToInt(StatusBarTimerLabelFontSize, _source.StatusBar.TimerLabelFontSize),
				TimerLabelWeight: StatusBarTimerLabelFontWeight,
				TimerLabelItalic: StatusBarTimerLabelItalic,
				TimerValueFontSize: ToInt(StatusBarTimerValueFontSize, _source.StatusBar.TimerValueFontSize),
				TimerValueWeight: StatusBarTimerValueFontWeight,
				TimerValueItalic: StatusBarTimerValueItalic),
			ValidationPanel: new ValidationPanelStyle(
				Background: ValidationPanelBackground.ToStyleColor(),
				Foreground: ValidationPanelForeground.ToStyleColor(),
				ErrorColor: ValidationPanelError.ToStyleColor(),
				WarningColor: ValidationPanelWarning.ToStyleColor(),
				MaxHeight: ToDouble(ValidationPanelMaxHeight, _source.ValidationPanel.MaxHeight)),
			Chrome: new ChromeColors(
				Info: Info.ToStyleColor(),
				Connected: Connected.ToStyleColor(),
				Disconnected: Disconnected.ToStyleColor(),
				LocalMode: LocalMode.ToStyleColor(),
				Connecting: Connecting.ToStyleColor(),
				PanelBackground: PanelBackground.ToStyleColor(),
				PanelHeaderBackground: PanelHeaderBackground.ToStyleColor(),
				SubtleBorder: SubtleBorder.ToStyleColor(),
				Separator: Separator.ToStyleColor(),
				SecondaryForeground: SecondaryForeground.ToStyleColor(),
				GridBorder: GridBorder.ToStyleColor(),
				GridBackground: GridBackground.ToStyleColor(),
				HeaderForeground: HeaderForeground.ToStyleColor(),
				GridLine: GridLine.ToStyleColor()),
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

	private void Seed(GridStyleOptions options)
	{
		_seeding = true;

		HeaderFontSize = options.Fonts.HeaderFontSize;
		CellFontSize = options.Fonts.CellFontSize;
		CellPaddingLeft = (decimal)options.Layout.CellPaddingLeft;
		CellPaddingTop = (decimal)options.Layout.CellPaddingTop;
		CellPaddingRight = (decimal)options.Layout.CellPaddingRight;
		CellPaddingBottom = (decimal)options.Layout.CellPaddingBottom;
		RowHeight = (decimal)options.Layout.RowHeight;
		StatusBarPadding = (decimal)options.StatusBar.Padding;
		StatusBarItemSpacing = (decimal)options.StatusBar.ItemSpacing;
		StatusBarFontSize = options.StatusBar.FontSize;
		StatusBarTimerLabelFontSize = options.StatusBar.TimerLabelFontSize;
		StatusBarTimerValueFontSize = options.StatusBar.TimerValueFontSize;
		ValidationPanelMaxHeight = (decimal)options.ValidationPanel.MaxHeight;

		FontFamily = options.Fonts.FontFamily;
		HeaderFontWeight = options.Fonts.HeaderFontWeight;
		HeaderItalic = options.Fonts.HeaderItalic;
		CellFontWeight = options.Fonts.CellFontWeight;
		CellItalic = options.Fonts.CellItalic;
		StatusBarFontWeight = options.StatusBar.Weight;
		StatusBarItalic = options.StatusBar.Italic;
		StatusBarTimerLabelFontWeight = options.StatusBar.TimerLabelWeight;
		StatusBarTimerLabelItalic = options.StatusBar.TimerLabelItalic;
		StatusBarTimerValueFontWeight = options.StatusBar.TimerValueWeight;
		StatusBarTimerValueItalic = options.StatusBar.TimerValueItalic;

		AvailableFontFamilies = BuildFontFamilies(options.Fonts.FontFamily);
		AvailableFontWeights = BuildFontWeights(options);

		SelectionBackground = options.Selection.Background.ToMediaColor();
		SelectionForeground = options.Selection.Foreground.ToMediaColor();
		CellChanged = options.ChangedCells.Changed.ToMediaColor();
		CellChangedSelected = options.ChangedCells.ChangedSelected.ToMediaColor();
		DisabledCellDepth0 = options.DisabledCells.Depth0.ToMediaColor();
		DisabledCellDepth1 = options.DisabledCells.Depth1.ToMediaColor();
		DisabledCellDepth2 = options.DisabledCells.Depth2.ToMediaColor();
		DisabledCellDepth3 = options.DisabledCells.Depth3.ToMediaColor();
		DisabledCellDepth0Past = options.DisabledCells.Depth0Past.ToMediaColor();
		DisabledCellDepth1Past = options.DisabledCells.Depth1Past.ToMediaColor();
		DisabledCellDepth2Past = options.DisabledCells.Depth2Past.ToMediaColor();
		DisabledCellDepth3Past = options.DisabledCells.Depth3Past.ToMediaColor();
		DisabledCellSelected = options.DisabledCells.Selected.ToMediaColor();
		DisabledCellForeground = options.DisabledCells.Foreground.ToMediaColor();
		ReadOnlyCellDepth0 = options.ReadOnlyCells.Depth0.ToMediaColor();
		ReadOnlyCellDepth1 = options.ReadOnlyCells.Depth1.ToMediaColor();
		ReadOnlyCellDepth2 = options.ReadOnlyCells.Depth2.ToMediaColor();
		ReadOnlyCellDepth3 = options.ReadOnlyCells.Depth3.ToMediaColor();
		ReadOnlyCellDepth0Past = options.ReadOnlyCells.Depth0Past.ToMediaColor();
		ReadOnlyCellDepth1Past = options.ReadOnlyCells.Depth1Past.ToMediaColor();
		ReadOnlyCellDepth2Past = options.ReadOnlyCells.Depth2Past.ToMediaColor();
		ReadOnlyCellDepth3Past = options.ReadOnlyCells.Depth3Past.ToMediaColor();
		ReadOnlyCellSelected = options.ReadOnlyCells.Selected.ToMediaColor();
		ReadOnlyCellForeground = options.ReadOnlyCells.Foreground.ToMediaColor();
		GridLine = options.Chrome.GridLine.ToMediaColor();
		StatusBarBackground = options.StatusBar.Background.ToMediaColor();
		StatusBarForeground = options.StatusBar.Foreground.ToMediaColor();
		ValidationPanelBackground = options.ValidationPanel.Background.ToMediaColor();
		ValidationPanelForeground = options.ValidationPanel.Foreground.ToMediaColor();
		ValidationPanelError = options.ValidationPanel.ErrorColor.ToMediaColor();
		ValidationPanelWarning = options.ValidationPanel.WarningColor.ToMediaColor();
		ExecutionDepth0 = options.Execution.Depth0.ToMediaColor();
		ExecutionDepth1 = options.Execution.Depth1.ToMediaColor();
		ExecutionDepth2 = options.Execution.Depth2.ToMediaColor();
		ExecutionDepth3 = options.Execution.Depth3.ToMediaColor();
		ExecutionDepth0Past = options.Execution.Depth0Past.ToMediaColor();
		ExecutionDepth1Past = options.Execution.Depth1Past.ToMediaColor();
		ExecutionDepth2Past = options.Execution.Depth2Past.ToMediaColor();
		ExecutionDepth3Past = options.Execution.Depth3Past.ToMediaColor();
		ExecutionCurrentStepMarker = options.Execution.CurrentStepMarker.ToMediaColor();
		Info = options.Chrome.Info.ToMediaColor();
		Connected = options.Chrome.Connected.ToMediaColor();
		Disconnected = options.Chrome.Disconnected.ToMediaColor();
		LocalMode = options.Chrome.LocalMode.ToMediaColor();
		Connecting = options.Chrome.Connecting.ToMediaColor();
		PanelBackground = options.Chrome.PanelBackground.ToMediaColor();
		PanelHeaderBackground = options.Chrome.PanelHeaderBackground.ToMediaColor();
		SubtleBorder = options.Chrome.SubtleBorder.ToMediaColor();
		Separator = options.Chrome.Separator.ToMediaColor();
		SecondaryForeground = options.Chrome.SecondaryForeground.ToMediaColor();
		GridBorder = options.Chrome.GridBorder.ToMediaColor();
		GridBackground = options.Chrome.GridBackground.ToMediaColor();
		HeaderForeground = options.Chrome.HeaderForeground.ToMediaColor();

		_seeding = false;
		RecomputeCanSave();
	}

	private void SetColor(ref Color field, Color value, [CallerMemberName] string? propertyName = null)
	{
		this.RaiseAndSetIfChanged(ref field, value, propertyName);
		RecomputeCanSave();
	}

	private void SetNumber(ref decimal? field, decimal? value, [CallerMemberName] string? propertyName = null)
	{
		this.RaiseAndSetIfChanged(ref field, value, propertyName);
		RecomputeCanSave();
	}

	private void SetValue<TValue>(ref TValue field, TValue value, [CallerMemberName] string? propertyName = null)
	{
		this.RaiseAndSetIfChanged(ref field, value, propertyName);
		RecomputeCanSave();
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
		if (_seeding)
		{
			return;
		}

		CanSave = NumericsInRange() && _gridStyleEditorFacade.Validate(BuildRecord()).IsSuccess;
	}

	private bool NumericsInRange()
	{
		return InRange(HeaderFontSize, MinFontSize, MaxFontSize)
			&& InRange(CellFontSize, MinFontSize, MaxFontSize)
			&& InRange(RowHeight, MinRowHeight, MaxRowHeight)
			&& InRange(CellPaddingLeft, MinPadding, MaxPadding)
			&& InRange(CellPaddingTop, MinPadding, MaxPadding)
			&& InRange(CellPaddingRight, MinPadding, MaxPadding)
			&& InRange(CellPaddingBottom, MinPadding, MaxPadding)
			&& InRange(StatusBarPadding, MinPadding, MaxPadding)
			&& InRange(StatusBarItemSpacing, MinSpacing, MaxSpacing)
			&& InRange(StatusBarFontSize, MinFontSize, MaxFontSize)
			&& InRange(StatusBarTimerLabelFontSize, MinFontSize, MaxFontSize)
			&& InRange(StatusBarTimerValueFontSize, MinFontSize, MaxFontSize)
			&& InRange(ValidationPanelMaxHeight, MinPanelMaxHeight, MaxPanelMaxHeight);
	}

	private static bool InRange(decimal? value, int min, int max)
	{
		return value is not null && value >= min && value <= max;
	}

	private static int ToInt(decimal? value, int fallback)
	{
		return value is null ? fallback : (int)Math.Round(value.Value, MidpointRounding.AwayFromZero);
	}

	private static double ToDouble(decimal? value, double fallback)
	{
		return value is null ? fallback : (double)value.Value;
	}
}
