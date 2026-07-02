using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

using Avalonia.Threading;

using FluentResults;

using ReactiveUI;

using SemiStep.Core.Shared;

using SemiStep.UI.Localization;

namespace SemiStep.UI.MessageService;

public class MessagePanelViewModel : ReactiveObject, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private readonly ObservableAsPropertyHelper<string> _errorCountText;
	private readonly ObservableAsPropertyHelper<bool> _hasErrors;
	private readonly ObservableAsPropertyHelper<bool> _hasStatusErrors;
	private readonly ObservableAsPropertyHelper<bool> _hasWarnings;
	private readonly ObservableAsPropertyHelper<bool> _showPanel;
	private readonly ObservableAsPropertyHelper<string> _statusErrorSummary;
	private readonly ObservableAsPropertyHelper<string> _warningCountText;

	private readonly List<MessageEntry> _validationEntries = [];

	private int _errorCount;
	private bool _hasEntries;
	private bool _isVisible = true;
	private MessageEntry? _operationEntry;
	private int _warningCount;

	public MessagePanelViewModel()
	{
		Entries = [];
		ToggleCommand = ReactiveCommand.Create(() => { IsVisible = !IsVisible; });

		_hasErrors = this.WhenAnyValue(x => x.ErrorCount)
			.Select(c => c > 0)
			.ToProperty(this, x => x.HasErrors)
			.DisposeWith(_disposables);

		_hasWarnings = this.WhenAnyValue(x => x.WarningCount)
			.Select(c => c > 0)
			.ToProperty(this, x => x.HasWarnings)
			.DisposeWith(_disposables);

		_hasStatusErrors = this.WhenAnyValue(x => x.ErrorCount, x => x.WarningCount)
			.Select(tuple => tuple.Item1 > 0 || tuple.Item2 > 0)
			.ToProperty(this, x => x.HasStatusErrors)
			.DisposeWith(_disposables);

		_errorCountText = this.WhenAnyValue(x => x.ErrorCount)
			.Select(FormatErrorCount)
			.ToProperty(this, x => x.ErrorCountText, initialValue: FormatErrorCount(0))
			.DisposeWith(_disposables);

		_warningCountText = this.WhenAnyValue(x => x.WarningCount)
			.Select(FormatWarningCount)
			.ToProperty(this, x => x.WarningCountText, initialValue: FormatWarningCount(0))
			.DisposeWith(_disposables);

		_statusErrorSummary = this.WhenAnyValue(
				x => x.HasErrors,
				x => x.HasWarnings,
				x => x.ErrorCountText,
				x => x.WarningCountText)
			.Select(tuple => (tuple.Item1, tuple.Item2) switch
			{
				(true, true) => $"{tuple.Item3}, {tuple.Item4}",
				(true, false) => tuple.Item3,
				(false, true) => tuple.Item4,
				_ => string.Empty
			})
			.ToProperty(this, x => x.StatusErrorSummary, initialValue: string.Empty)
			.DisposeWith(_disposables);

		_showPanel = this.WhenAnyValue(x => x.HasEntries, x => x.IsVisible)
			.Select(tuple => tuple.Item1 && tuple.Item2)
			.ToProperty(this, x => x.ShowPanel)
			.DisposeWith(_disposables);
	}

	public ObservableCollection<MessageEntry> Entries { get; }

	public ReactiveCommand<Unit, Unit> ToggleCommand { get; }

	public int ErrorCount
	{
		get => _errorCount;
		private set => this.RaiseAndSetIfChanged(ref _errorCount, value);
	}

	public int WarningCount
	{
		get => _warningCount;
		private set => this.RaiseAndSetIfChanged(ref _warningCount, value);
	}

	public bool HasEntries
	{
		get => _hasEntries;
		private set => this.RaiseAndSetIfChanged(ref _hasEntries, value);
	}

	public bool HasErrors => _hasErrors.Value;

	public bool HasWarnings => _hasWarnings.Value;

	public bool HasStatusErrors => _hasStatusErrors.Value;

	public string ErrorCountText => _errorCountText.Value;

	public string WarningCountText => _warningCountText.Value;

	public string StatusErrorSummary => _statusErrorSummary.Value;

	public bool IsVisible
	{
		get => _isVisible;
		set => this.RaiseAndSetIfChanged(ref _isVisible, value);
	}

	public bool ShowPanel => _showPanel.Value;

	public void Dispose()
	{
		_disposables.Dispose();
		ToggleCommand.Dispose();
		GC.SuppressFinalize(this);
	}

	public void RefreshReasons(IEnumerable<IReason> reasons)
	{
		ArgumentNullException.ThrowIfNull(reasons);
		var reasonList = reasons.ToList();
		PostOnUiThread(() =>
		{
			_validationEntries.Clear();

			foreach (var error in reasonList.OfType<IError>())
			{
				_validationEntries.Add(new MessageEntry(MessageSeverity.Error, error.Message));
			}

			foreach (var warning in reasonList.OfType<Warning>())
			{
				_validationEntries.Add(new MessageEntry(MessageSeverity.Warning, warning.Message));
			}

			Rebuild();
		});
	}

	public void ReportSuccess(string message)
	{
		ReportOperation(MessageSeverity.Info, message);
	}

	public void ReportWarning(string message)
	{
		ReportOperation(MessageSeverity.Warning, message);
	}

	public void ReportError(string message)
	{
		ReportOperation(MessageSeverity.Error, message);
	}

	public void ClearOperation()
	{
		PostOnUiThread(() =>
		{
			_operationEntry = null;
			Rebuild();
		});
	}

	private void ReportOperation(MessageSeverity severity, string message)
	{
		PostOnUiThread(() =>
		{
			_operationEntry = new MessageEntry(severity, message);
			Rebuild();
		});
	}

	private void Rebuild()
	{
		Entries.Clear();

		if (_operationEntry is not null)
		{
			Entries.Add(_operationEntry);
		}

		foreach (var entry in _validationEntries)
		{
			Entries.Add(entry);
		}

		RecountAndNotify();
	}

	private void PostOnUiThread(Action action)
	{
		if (Dispatcher.UIThread.CheckAccess())
		{
			action();
		}
		else
		{
			Dispatcher.UIThread.Post(action);
		}
	}

	private void RecountAndNotify()
	{
		ErrorCount = _validationEntries.Count(e => e.IsError);
		WarningCount = _validationEntries.Count(e => e.IsWarning);
		HasEntries = _validationEntries.Count > 0
			|| _operationEntry is { Severity: MessageSeverity.Error or MessageSeverity.Warning };
	}

	internal static string FormatErrorCount(int count)
	{
		return string.Format(CultureInfo.InvariantCulture, Resources.MessagePanelErrorsFormat, count);
	}

	internal static string FormatWarningCount(int count)
	{
		return string.Format(CultureInfo.InvariantCulture, Resources.MessagePanelWarningsFormat, count);
	}
}
