using System;
using System.Reactive;
using System.Runtime.CompilerServices;

using Avalonia.Media;

using FluentResults;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.UI.Localization;

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
				Background: HexColor.ToHex(SelectionBackground),
				Foreground: HexColor.ToHex(SelectionForeground)),
			ChangedCells: new ChangedCellColors(
				Changed: HexColor.ToHex(CellChanged),
				ChangedSelected: HexColor.ToHex(CellChangedSelected)),
			ReadOnlyCells: new DepthPalette(
				Depth0: HexColor.ToHex(ReadOnlyCellDepth0),
				Depth1: HexColor.ToHex(ReadOnlyCellDepth1),
				Depth2: HexColor.ToHex(ReadOnlyCellDepth2),
				Depth3: HexColor.ToHex(ReadOnlyCellDepth3),
				Depth0Past: HexColor.ToHex(ReadOnlyCellDepth0Past),
				Depth1Past: HexColor.ToHex(ReadOnlyCellDepth1Past),
				Depth2Past: HexColor.ToHex(ReadOnlyCellDepth2Past),
				Depth3Past: HexColor.ToHex(ReadOnlyCellDepth3Past),
				Selected: HexColor.ToHex(ReadOnlyCellSelected),
				Foreground: HexColor.ToHex(ReadOnlyCellForeground)),
			DisabledCells: new DepthPalette(
				Depth0: HexColor.ToHex(DisabledCellDepth0),
				Depth1: HexColor.ToHex(DisabledCellDepth1),
				Depth2: HexColor.ToHex(DisabledCellDepth2),
				Depth3: HexColor.ToHex(DisabledCellDepth3),
				Depth0Past: HexColor.ToHex(DisabledCellDepth0Past),
				Depth1Past: HexColor.ToHex(DisabledCellDepth1Past),
				Depth2Past: HexColor.ToHex(DisabledCellDepth2Past),
				Depth3Past: HexColor.ToHex(DisabledCellDepth3Past),
				Selected: HexColor.ToHex(DisabledCellSelected),
				Foreground: HexColor.ToHex(DisabledCellForeground)),
			Execution: new ExecutionPalette(
				Depth0: HexColor.ToHex(ExecutionDepth0),
				Depth1: HexColor.ToHex(ExecutionDepth1),
				Depth2: HexColor.ToHex(ExecutionDepth2),
				Depth3: HexColor.ToHex(ExecutionDepth3),
				Depth0Past: HexColor.ToHex(ExecutionDepth0Past),
				Depth1Past: HexColor.ToHex(ExecutionDepth1Past),
				Depth2Past: HexColor.ToHex(ExecutionDepth2Past),
				Depth3Past: HexColor.ToHex(ExecutionDepth3Past),
				CurrentStepMarker: HexColor.ToHex(ExecutionCurrentStepMarker)),
			StatusBar: new StatusBarStyle(
				Background: HexColor.ToHex(StatusBarBackground),
				Foreground: HexColor.ToHex(StatusBarForeground),
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
				Background: HexColor.ToHex(ValidationPanelBackground),
				Foreground: HexColor.ToHex(ValidationPanelForeground),
				ErrorColor: HexColor.ToHex(ValidationPanelError),
				WarningColor: HexColor.ToHex(ValidationPanelWarning),
				MaxHeight: ToDouble(ValidationPanelMaxHeight, _source.ValidationPanel.MaxHeight)),
			Chrome: new ChromeColors(
				Info: HexColor.ToHex(Info),
				Connected: HexColor.ToHex(Connected),
				Disconnected: HexColor.ToHex(Disconnected),
				LocalMode: HexColor.ToHex(LocalMode),
				Connecting: HexColor.ToHex(Connecting),
				PanelBackground: HexColor.ToHex(PanelBackground),
				PanelHeaderBackground: HexColor.ToHex(PanelHeaderBackground),
				SubtleBorder: HexColor.ToHex(SubtleBorder),
				Separator: HexColor.ToHex(Separator),
				SecondaryForeground: HexColor.ToHex(SecondaryForeground),
				GridBorder: HexColor.ToHex(GridBorder),
				GridBackground: HexColor.ToHex(GridBackground),
				HeaderForeground: HexColor.ToHex(HeaderForeground),
				GridLine: HexColor.ToHex(GridLine)),
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

		SelectionBackground = HexColor.Parse(options.Selection.Background);
		SelectionForeground = HexColor.Parse(options.Selection.Foreground);
		CellChanged = HexColor.Parse(options.ChangedCells.Changed);
		CellChangedSelected = HexColor.Parse(options.ChangedCells.ChangedSelected);
		DisabledCellDepth0 = HexColor.Parse(options.DisabledCells.Depth0);
		DisabledCellDepth1 = HexColor.Parse(options.DisabledCells.Depth1);
		DisabledCellDepth2 = HexColor.Parse(options.DisabledCells.Depth2);
		DisabledCellDepth3 = HexColor.Parse(options.DisabledCells.Depth3);
		DisabledCellDepth0Past = HexColor.Parse(options.DisabledCells.Depth0Past);
		DisabledCellDepth1Past = HexColor.Parse(options.DisabledCells.Depth1Past);
		DisabledCellDepth2Past = HexColor.Parse(options.DisabledCells.Depth2Past);
		DisabledCellDepth3Past = HexColor.Parse(options.DisabledCells.Depth3Past);
		DisabledCellSelected = HexColor.Parse(options.DisabledCells.Selected);
		DisabledCellForeground = HexColor.Parse(options.DisabledCells.Foreground);
		ReadOnlyCellDepth0 = HexColor.Parse(options.ReadOnlyCells.Depth0);
		ReadOnlyCellDepth1 = HexColor.Parse(options.ReadOnlyCells.Depth1);
		ReadOnlyCellDepth2 = HexColor.Parse(options.ReadOnlyCells.Depth2);
		ReadOnlyCellDepth3 = HexColor.Parse(options.ReadOnlyCells.Depth3);
		ReadOnlyCellDepth0Past = HexColor.Parse(options.ReadOnlyCells.Depth0Past);
		ReadOnlyCellDepth1Past = HexColor.Parse(options.ReadOnlyCells.Depth1Past);
		ReadOnlyCellDepth2Past = HexColor.Parse(options.ReadOnlyCells.Depth2Past);
		ReadOnlyCellDepth3Past = HexColor.Parse(options.ReadOnlyCells.Depth3Past);
		ReadOnlyCellSelected = HexColor.Parse(options.ReadOnlyCells.Selected);
		ReadOnlyCellForeground = HexColor.Parse(options.ReadOnlyCells.Foreground);
		GridLine = HexColor.Parse(options.Chrome.GridLine);
		StatusBarBackground = HexColor.Parse(options.StatusBar.Background);
		StatusBarForeground = HexColor.Parse(options.StatusBar.Foreground);
		ValidationPanelBackground = HexColor.Parse(options.ValidationPanel.Background);
		ValidationPanelForeground = HexColor.Parse(options.ValidationPanel.Foreground);
		ValidationPanelError = HexColor.Parse(options.ValidationPanel.ErrorColor);
		ValidationPanelWarning = HexColor.Parse(options.ValidationPanel.WarningColor);
		ExecutionDepth0 = HexColor.Parse(options.Execution.Depth0);
		ExecutionDepth1 = HexColor.Parse(options.Execution.Depth1);
		ExecutionDepth2 = HexColor.Parse(options.Execution.Depth2);
		ExecutionDepth3 = HexColor.Parse(options.Execution.Depth3);
		ExecutionDepth0Past = HexColor.Parse(options.Execution.Depth0Past);
		ExecutionDepth1Past = HexColor.Parse(options.Execution.Depth1Past);
		ExecutionDepth2Past = HexColor.Parse(options.Execution.Depth2Past);
		ExecutionDepth3Past = HexColor.Parse(options.Execution.Depth3Past);
		ExecutionCurrentStepMarker = HexColor.Parse(options.Execution.CurrentStepMarker);
		Info = HexColor.Parse(options.Chrome.Info);
		Connected = HexColor.Parse(options.Chrome.Connected);
		Disconnected = HexColor.Parse(options.Chrome.Disconnected);
		LocalMode = HexColor.Parse(options.Chrome.LocalMode);
		Connecting = HexColor.Parse(options.Chrome.Connecting);
		PanelBackground = HexColor.Parse(options.Chrome.PanelBackground);
		PanelHeaderBackground = HexColor.Parse(options.Chrome.PanelHeaderBackground);
		SubtleBorder = HexColor.Parse(options.Chrome.SubtleBorder);
		Separator = HexColor.Parse(options.Chrome.Separator);
		SecondaryForeground = HexColor.Parse(options.Chrome.SecondaryForeground);
		GridBorder = HexColor.Parse(options.Chrome.GridBorder);
		GridBackground = HexColor.Parse(options.Chrome.GridBackground);
		HeaderForeground = HexColor.Parse(options.Chrome.HeaderForeground);

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
