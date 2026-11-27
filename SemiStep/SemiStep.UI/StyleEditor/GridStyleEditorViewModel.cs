using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using Avalonia.Media;

using FluentResults;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.UI.Localization;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// The style editor: one draft per <see cref="GridStyleOptions"/> group, validated field by field in page order;
/// see docs/architecture/grid-style-configuration.md, "Editor write-back".
/// </summary>
public sealed class GridStyleEditorViewModel : ReactiveObject, IDisposable
{
	private readonly IGridStyleEditorFacade _gridStyleEditorFacade;
	private readonly string _configDir;
	private readonly ILogger<GridStyleEditorViewModel> _logger;
	private readonly ObservableAsPropertyHelper<string?> _message;
	private readonly ObservableAsPropertyHelper<bool> _isMessageError;

	private GridOrientation _orientation;

	// Null after a failed load: there is no file content to compare with, so the next Save writes.
	private GridStyleOptions? _saved;
	private IReadOnlyList<EditorField> _fields = [];
	private IDisposable _draftSubscription = Disposable.Empty;

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
		_orientation = source.Orientation;
		_saved = source;
		_logger = logger;

		SaveCommand = ReactiveCommand.CreateFromTask(
			SaveAsync,
			this.WhenAnyValue(viewModel => viewModel.CanSave));

		// A modal editor shows a save fault on its own message line, not on the main window's panel
		// hidden behind it.
		SaveCommand.ThrownExceptions.Subscribe(ReportSaveException);

		RestoreDefaultsCommand = ReactiveCommand.Create(RestoreDefaults);

		_message = this
			.WhenAnyValue(
				viewModel => viewModel.ValidationMessage,
				viewModel => viewModel.ErrorMessage,
				viewModel => viewModel.IsRestartPending,
				ComposeMessage)
			.ToProperty(this, viewModel => viewModel.Message);

		_isMessageError = this
			.WhenAnyValue(
				viewModel => viewModel.ValidationMessage,
				viewModel => viewModel.ErrorMessage,
				(validation, error) => validation is not null || error is not null)
			.ToProperty(this, viewModel => viewModel.IsMessageError);

		ReplaceDrafts(source);
		SeedPickers(source);
	}

	/// <summary>Writes the drafts to the file; true when the file was written.</summary>
	public ReactiveCommand<Unit, bool> SaveCommand { get; }

	public ReactiveCommand<Unit, Unit> RestoreDefaultsCommand { get; }

	public StyleEditorSections Sections { get; } = new();

	/// <summary>The last load or save failure.</summary>
	public string? ErrorMessage
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	/// <summary>The first invalid field in page order, or null while every field is valid.</summary>
	public string? ValidationMessage
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	/// <summary>True after a save that wrote a change; the change applies on the next start.</summary>
	public bool IsRestartPending
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	/// <summary>The footer's message line: a validation message, then a failure, then the restart notice.</summary>
	public string? Message => _message.Value;

	public bool IsMessageError => _isMessageError.Value;

	public bool CanSave
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	// Raising change on a whole-draft swap re-evaluates every bound leaf path, so a reseed
	// (load or defaults) refreshes the editor.
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

	public GreyCellColorsDraft GreyCells
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public ExecutionPaletteDraft Execution
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public LoopPaletteDraft Loop
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
			_saved = null;
			ErrorMessage = string.Join("; ", result.Errors.Select(ReasonLocalizer.Localize));
			LogCausedByExceptions(result, "Grid style load failed");
			return;
		}

		_orientation = result.Value.Orientation;
		_saved = result.Value;
		ErrorMessage = null;
		ReplaceDrafts(result.Value);
		SeedPickers(result.Value);
	}

	public GridStyleOptions BuildRecord()
	{
		return new GridStyleOptions(
			Fonts: Fonts.Build(),
			Layout: Layout.Build(),
			Selection: Selection.Build(),
			ChangedCells: ChangedCells.Build(),
			ReadOnlyCell: GreyCells.ReadOnly.ToStyleColor(),
			DisabledCell: GreyCells.Disabled.ToStyleColor(),
			Execution: Execution.Build(),
			Loop: Loop.Build(),
			StatusBar: StatusBar.Build(),
			ValidationPanel: ValidationPanel.Build(),
			Chrome: Chrome.Build(),
			Orientation: _orientation);
	}

	private async Task<bool> SaveAsync()
	{
		var record = BuildRecord();
		if (record == _saved)
		{
			ErrorMessage = null;
			return false;
		}

		var result = await _gridStyleEditorFacade.Save(_configDir, record);
		if (result.IsFailed)
		{
			ErrorMessage = string.Join("; ", result.Errors.Select(ReasonLocalizer.Localize));
			LogCausedByExceptions(result, "Grid style save failed");
			return false;
		}

		_saved = record;
		ErrorMessage = null;
		IsRestartPending = true;
		return true;
	}

	// The two chrome keys without a row keep their current value, so a key the operator cannot see
	// never changes; BuildRecord takes the orientation from the loaded record.
	private void RestoreDefaults()
	{
		var defaults = GridStyleOptions.Default;
		ReplaceDrafts(defaults with
		{
			Chrome = defaults.Chrome with { Info = Chrome.Info, PanelBackground = Chrome.PanelBackground }
		});
	}

	private void ReportSaveException(Exception exception)
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

	private static string? ComposeMessage(string? validationMessage, string? errorMessage, bool isRestartPending)
	{
		return validationMessage
			?? errorMessage
			?? (isRestartPending ? Resources.EditorSavedRestartNotice : null);
	}

	// Swaps the draft subscription so a stale draft never keeps driving validation.
	[MemberNotNull(
		nameof(Fonts),
		nameof(Layout),
		nameof(Selection),
		nameof(ChangedCells),
		nameof(GreyCells),
		nameof(Execution),
		nameof(Loop),
		nameof(StatusBar),
		nameof(ValidationPanel),
		nameof(Chrome))]
	private void ReplaceDrafts(GridStyleOptions options)
	{
		Fonts = new GridStyleFontsDraft(options.Fonts);
		Layout = new GridStyleLayoutDraft(options.Layout);
		Selection = new SelectionColorsDraft(options.Selection);
		ChangedCells = new ChangedCellColorsDraft(options.ChangedCells);
		GreyCells = new GreyCellColorsDraft(options.ReadOnlyCell, options.DisabledCell);
		Execution = new ExecutionPaletteDraft(options.Execution);
		Loop = new LoopPaletteDraft(options.Loop);
		StatusBar = new StatusBarStyleDraft(options.StatusBar);
		ValidationPanel = new ValidationPanelStyleDraft(options.ValidationPanel);
		Chrome = new ChromeColorsDraft(options.Chrome);

		_fields = EditorFieldList.Build(this);
		_draftSubscription.Dispose();
		_draftSubscription = _fields
			.Select(field => field.Draft.Changed)
			.Concat(EditorFieldList.ColorDrafts(this).Select(draft => draft.Changed))
			.Append(Fonts.Changed)
			.Append(StatusBar.Changed)
			.Merge()
			.Subscribe(_ => OnDraftChanged());

		OnDraftChanged();
	}

	// An edit after a save makes the drafts differ from the file again, so the restart notice no longer applies.
	private void OnDraftChanged()
	{
		IsRestartPending = false;
		Revalidate();
	}

	private void Revalidate()
	{
		var firstInvalid = _fields.FirstOrDefault(field => !field.IsValid);
		ValidationMessage = firstInvalid?.Message;
		CanSave = firstInvalid is null;

		foreach (var section in Sections.All)
		{
			section.HasError = _fields.Any(field => field.Section == section && !field.IsValid);
		}
	}

	private void SeedPickers(GridStyleOptions options)
	{
		AvailableFontFamilies = BuildFontFamilies(options.Fonts.FontFamily);
		AvailableFontWeights = BuildFontWeights(options);
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
			new(300, Resources.EditorWeightLight),
			new(400, Resources.EditorWeightNormal),
			new(500, Resources.EditorWeightMedium),
			new(600, Resources.EditorWeightSemibold),
			new(700, Resources.EditorWeightBold),
			new(800, Resources.EditorWeightExtraBold),
			new(900, Resources.EditorWeightBlack)
		};

		var seededWeights = new[]
		{
			options.Fonts.HeaderFontWeight,
			options.Fonts.CellFontWeight,
			options.StatusBar.Weight
		};

		foreach (var weight in seededWeights.Distinct())
		{
			if (weights.All(option => option.Value != weight))
			{
				weights.Add(new FontWeightOption(weight, weight.ToString(CultureInfo.InvariantCulture)));
			}
		}

		return weights;
	}

	public void Dispose()
	{
		_draftSubscription.Dispose();
		_message.Dispose();
		_isMessageError.Dispose();
	}
}
