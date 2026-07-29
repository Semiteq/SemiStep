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
/// colors-only). The editor surfaces effectively the whole record; <see cref="BuildRecord"/> rebuilds
/// it via <c>with</c> over the seeded source, so any field that is not surfaced is preserved.
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

		SaveCommand = ReactiveCommand.Create(
			Save,
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
			ErrorMessage = string.Join("; ", result.Errors.Select(error => error.Message));
			return;
		}

		_source = result.Value;
		ErrorMessage = null;
		Seed(result.Value);
	}

	public GridStyleOptions BuildRecord()
	{
		return _source with
		{
			HeaderFontSize = ToInt(HeaderFontSize, _source.HeaderFontSize),
			CellFontSize = ToInt(CellFontSize, _source.CellFontSize),
			CellPaddingLeft = ToDouble(CellPaddingLeft, _source.CellPaddingLeft),
			CellPaddingTop = ToDouble(CellPaddingTop, _source.CellPaddingTop),
			CellPaddingRight = ToDouble(CellPaddingRight, _source.CellPaddingRight),
			CellPaddingBottom = ToDouble(CellPaddingBottom, _source.CellPaddingBottom),
			RowHeight = ToDouble(RowHeight, _source.RowHeight),
			StatusBarPadding = ToDouble(StatusBarPadding, _source.StatusBarPadding),
			StatusBarItemSpacing = ToDouble(StatusBarItemSpacing, _source.StatusBarItemSpacing),
			StatusBarFontSize = ToInt(StatusBarFontSize, _source.StatusBarFontSize),
			StatusBarTimerLabelFontSize = ToInt(StatusBarTimerLabelFontSize, _source.StatusBarTimerLabelFontSize),
			StatusBarTimerValueFontSize = ToInt(StatusBarTimerValueFontSize, _source.StatusBarTimerValueFontSize),
			ValidationPanelMaxHeight = ToDouble(ValidationPanelMaxHeight, _source.ValidationPanelMaxHeight),
			FontFamily = FontFamily ?? _source.FontFamily,
			HeaderFontWeight = HeaderFontWeight,
			HeaderItalic = HeaderItalic,
			CellFontWeight = CellFontWeight,
			CellItalic = CellItalic,
			StatusBarFontWeight = StatusBarFontWeight,
			StatusBarItalic = StatusBarItalic,
			StatusBarTimerLabelFontWeight = StatusBarTimerLabelFontWeight,
			StatusBarTimerLabelItalic = StatusBarTimerLabelItalic,
			StatusBarTimerValueFontWeight = StatusBarTimerValueFontWeight,
			StatusBarTimerValueItalic = StatusBarTimerValueItalic,
			SelectionBackgroundColor = HexColor.ToHex(SelectionBackground),
			SelectionForegroundColor = HexColor.ToHex(SelectionForeground),
			CellChangedColor = HexColor.ToHex(CellChanged),
			CellChangedSelectedColor = HexColor.ToHex(CellChangedSelected),
			DisabledCellDepth0Color = HexColor.ToHex(DisabledCellDepth0),
			DisabledCellDepth1Color = HexColor.ToHex(DisabledCellDepth1),
			DisabledCellDepth2Color = HexColor.ToHex(DisabledCellDepth2),
			DisabledCellDepth3Color = HexColor.ToHex(DisabledCellDepth3),
			DisabledCellDepth0PastColor = HexColor.ToHex(DisabledCellDepth0Past),
			DisabledCellDepth1PastColor = HexColor.ToHex(DisabledCellDepth1Past),
			DisabledCellDepth2PastColor = HexColor.ToHex(DisabledCellDepth2Past),
			DisabledCellDepth3PastColor = HexColor.ToHex(DisabledCellDepth3Past),
			DisabledCellSelectedColor = HexColor.ToHex(DisabledCellSelected),
			DisabledCellForegroundColor = HexColor.ToHex(DisabledCellForeground),
			ReadOnlyCellDepth0Color = HexColor.ToHex(ReadOnlyCellDepth0),
			ReadOnlyCellDepth1Color = HexColor.ToHex(ReadOnlyCellDepth1),
			ReadOnlyCellDepth2Color = HexColor.ToHex(ReadOnlyCellDepth2),
			ReadOnlyCellDepth3Color = HexColor.ToHex(ReadOnlyCellDepth3),
			ReadOnlyCellDepth0PastColor = HexColor.ToHex(ReadOnlyCellDepth0Past),
			ReadOnlyCellDepth1PastColor = HexColor.ToHex(ReadOnlyCellDepth1Past),
			ReadOnlyCellDepth2PastColor = HexColor.ToHex(ReadOnlyCellDepth2Past),
			ReadOnlyCellDepth3PastColor = HexColor.ToHex(ReadOnlyCellDepth3Past),
			ReadOnlyCellSelectedColor = HexColor.ToHex(ReadOnlyCellSelected),
			ReadOnlyCellForegroundColor = HexColor.ToHex(ReadOnlyCellForeground),
			GridLineColor = HexColor.ToHex(GridLine),
			StatusBarBackgroundColor = HexColor.ToHex(StatusBarBackground),
			StatusBarForegroundColor = HexColor.ToHex(StatusBarForeground),
			ValidationPanelBackgroundColor = HexColor.ToHex(ValidationPanelBackground),
			ValidationPanelForegroundColor = HexColor.ToHex(ValidationPanelForeground),
			ValidationPanelErrorColor = HexColor.ToHex(ValidationPanelError),
			ValidationPanelWarningColor = HexColor.ToHex(ValidationPanelWarning),
			ExecutionDepth0Color = HexColor.ToHex(ExecutionDepth0),
			ExecutionDepth1Color = HexColor.ToHex(ExecutionDepth1),
			ExecutionDepth2Color = HexColor.ToHex(ExecutionDepth2),
			ExecutionDepth3Color = HexColor.ToHex(ExecutionDepth3),
			ExecutionDepth0PastColor = HexColor.ToHex(ExecutionDepth0Past),
			ExecutionDepth1PastColor = HexColor.ToHex(ExecutionDepth1Past),
			ExecutionDepth2PastColor = HexColor.ToHex(ExecutionDepth2Past),
			ExecutionDepth3PastColor = HexColor.ToHex(ExecutionDepth3Past),
			ExecutionCurrentStepMarkerColor = HexColor.ToHex(ExecutionCurrentStepMarker),
			InfoColor = HexColor.ToHex(Info),
			ConnectedColor = HexColor.ToHex(Connected),
			DisconnectedColor = HexColor.ToHex(Disconnected),
			LocalModeColor = HexColor.ToHex(LocalMode),
			ConnectingColor = HexColor.ToHex(Connecting),
			PanelBackgroundColor = HexColor.ToHex(PanelBackground),
			PanelHeaderBackgroundColor = HexColor.ToHex(PanelHeaderBackground),
			SubtleBorderColor = HexColor.ToHex(SubtleBorder),
			SeparatorColor = HexColor.ToHex(Separator),
			SecondaryForegroundColor = HexColor.ToHex(SecondaryForeground),
			GridBorderColor = HexColor.ToHex(GridBorder),
			GridBackgroundColor = HexColor.ToHex(GridBackground),
			HeaderForegroundColor = HexColor.ToHex(HeaderForeground)
		};
	}

	private bool Save()
	{
		if (!CanSave)
		{
			ErrorMessage = Resources.EditorCannotSave;
			return false;
		}

		var result = _gridStyleEditorFacade.Save(_configDir, BuildRecord());
		if (result.IsFailed)
		{
			ErrorMessage = string.Join("; ", result.Errors.Select(error => error.Message));
			return false;
		}

		ErrorMessage = null;
		return true;
	}

	internal void ReportSaveException(Exception exception)
	{
		_logger.LogError(exception, "Style editor save failed");
		ErrorMessage = $"{Resources.SaveFailed}: {exception.Message}";
	}

	private void Seed(GridStyleOptions options)
	{
		_seeding = true;

		HeaderFontSize = options.HeaderFontSize;
		CellFontSize = options.CellFontSize;
		CellPaddingLeft = (decimal)options.CellPaddingLeft;
		CellPaddingTop = (decimal)options.CellPaddingTop;
		CellPaddingRight = (decimal)options.CellPaddingRight;
		CellPaddingBottom = (decimal)options.CellPaddingBottom;
		RowHeight = (decimal)options.RowHeight;
		StatusBarPadding = (decimal)options.StatusBarPadding;
		StatusBarItemSpacing = (decimal)options.StatusBarItemSpacing;
		StatusBarFontSize = options.StatusBarFontSize;
		StatusBarTimerLabelFontSize = options.StatusBarTimerLabelFontSize;
		StatusBarTimerValueFontSize = options.StatusBarTimerValueFontSize;
		ValidationPanelMaxHeight = (decimal)options.ValidationPanelMaxHeight;

		FontFamily = options.FontFamily;
		HeaderFontWeight = options.HeaderFontWeight;
		HeaderItalic = options.HeaderItalic;
		CellFontWeight = options.CellFontWeight;
		CellItalic = options.CellItalic;
		StatusBarFontWeight = options.StatusBarFontWeight;
		StatusBarItalic = options.StatusBarItalic;
		StatusBarTimerLabelFontWeight = options.StatusBarTimerLabelFontWeight;
		StatusBarTimerLabelItalic = options.StatusBarTimerLabelItalic;
		StatusBarTimerValueFontWeight = options.StatusBarTimerValueFontWeight;
		StatusBarTimerValueItalic = options.StatusBarTimerValueItalic;

		AvailableFontFamilies = BuildFontFamilies(options.FontFamily);
		AvailableFontWeights = BuildFontWeights(options);

		SelectionBackground = HexColor.Parse(options.SelectionBackgroundColor);
		SelectionForeground = HexColor.Parse(options.SelectionForegroundColor);
		CellChanged = HexColor.Parse(options.CellChangedColor);
		CellChangedSelected = HexColor.Parse(options.CellChangedSelectedColor);
		DisabledCellDepth0 = HexColor.Parse(options.DisabledCellDepth0Color);
		DisabledCellDepth1 = HexColor.Parse(options.DisabledCellDepth1Color);
		DisabledCellDepth2 = HexColor.Parse(options.DisabledCellDepth2Color);
		DisabledCellDepth3 = HexColor.Parse(options.DisabledCellDepth3Color);
		DisabledCellDepth0Past = HexColor.Parse(options.DisabledCellDepth0PastColor);
		DisabledCellDepth1Past = HexColor.Parse(options.DisabledCellDepth1PastColor);
		DisabledCellDepth2Past = HexColor.Parse(options.DisabledCellDepth2PastColor);
		DisabledCellDepth3Past = HexColor.Parse(options.DisabledCellDepth3PastColor);
		DisabledCellSelected = HexColor.Parse(options.DisabledCellSelectedColor);
		DisabledCellForeground = HexColor.Parse(options.DisabledCellForegroundColor);
		ReadOnlyCellDepth0 = HexColor.Parse(options.ReadOnlyCellDepth0Color);
		ReadOnlyCellDepth1 = HexColor.Parse(options.ReadOnlyCellDepth1Color);
		ReadOnlyCellDepth2 = HexColor.Parse(options.ReadOnlyCellDepth2Color);
		ReadOnlyCellDepth3 = HexColor.Parse(options.ReadOnlyCellDepth3Color);
		ReadOnlyCellDepth0Past = HexColor.Parse(options.ReadOnlyCellDepth0PastColor);
		ReadOnlyCellDepth1Past = HexColor.Parse(options.ReadOnlyCellDepth1PastColor);
		ReadOnlyCellDepth2Past = HexColor.Parse(options.ReadOnlyCellDepth2PastColor);
		ReadOnlyCellDepth3Past = HexColor.Parse(options.ReadOnlyCellDepth3PastColor);
		ReadOnlyCellSelected = HexColor.Parse(options.ReadOnlyCellSelectedColor);
		ReadOnlyCellForeground = HexColor.Parse(options.ReadOnlyCellForegroundColor);
		GridLine = HexColor.Parse(options.GridLineColor);
		StatusBarBackground = HexColor.Parse(options.StatusBarBackgroundColor);
		StatusBarForeground = HexColor.Parse(options.StatusBarForegroundColor);
		ValidationPanelBackground = HexColor.Parse(options.ValidationPanelBackgroundColor);
		ValidationPanelForeground = HexColor.Parse(options.ValidationPanelForegroundColor);
		ValidationPanelError = HexColor.Parse(options.ValidationPanelErrorColor);
		ValidationPanelWarning = HexColor.Parse(options.ValidationPanelWarningColor);
		ExecutionDepth0 = HexColor.Parse(options.ExecutionDepth0Color);
		ExecutionDepth1 = HexColor.Parse(options.ExecutionDepth1Color);
		ExecutionDepth2 = HexColor.Parse(options.ExecutionDepth2Color);
		ExecutionDepth3 = HexColor.Parse(options.ExecutionDepth3Color);
		ExecutionDepth0Past = HexColor.Parse(options.ExecutionDepth0PastColor);
		ExecutionDepth1Past = HexColor.Parse(options.ExecutionDepth1PastColor);
		ExecutionDepth2Past = HexColor.Parse(options.ExecutionDepth2PastColor);
		ExecutionDepth3Past = HexColor.Parse(options.ExecutionDepth3PastColor);
		ExecutionCurrentStepMarker = HexColor.Parse(options.ExecutionCurrentStepMarkerColor);
		Info = HexColor.Parse(options.InfoColor);
		Connected = HexColor.Parse(options.ConnectedColor);
		Disconnected = HexColor.Parse(options.DisconnectedColor);
		LocalMode = HexColor.Parse(options.LocalModeColor);
		Connecting = HexColor.Parse(options.ConnectingColor);
		PanelBackground = HexColor.Parse(options.PanelBackgroundColor);
		PanelHeaderBackground = HexColor.Parse(options.PanelHeaderBackgroundColor);
		SubtleBorder = HexColor.Parse(options.SubtleBorderColor);
		Separator = HexColor.Parse(options.SeparatorColor);
		SecondaryForeground = HexColor.Parse(options.SecondaryForegroundColor);
		GridBorder = HexColor.Parse(options.GridBorderColor);
		GridBackground = HexColor.Parse(options.GridBackgroundColor);
		HeaderForeground = HexColor.Parse(options.HeaderForegroundColor);

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
			options.HeaderFontWeight,
			options.CellFontWeight,
			options.StatusBarFontWeight,
			options.StatusBarTimerLabelFontWeight,
			options.StatusBarTimerValueFontWeight
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
