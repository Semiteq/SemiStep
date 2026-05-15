using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

using Avalonia.Threading;

using FluentResults;

using ReactiveUI;

using SemiStep.Core.Shared;

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

	private int _errorCount;
	private bool _hasEntries;
	private bool _isVisible = true;
	private int _warningCount;

	public MessagePanelViewModel()
	{
		Entries = [];
		ClearCommand = ReactiveCommand.Create(ClearNonStructural);
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
			.Select(c => $"{c} {(c == 1 ? "Error" : "Errors")}")
			.ToProperty(this, x => x.ErrorCountText, initialValue: "0 Errors")
			.DisposeWith(_disposables);

		_warningCountText = this.WhenAnyValue(x => x.WarningCount)
			.Select(c => $"{c} {(c == 1 ? "Warning" : "Warnings")}")
			.ToProperty(this, x => x.WarningCountText, initialValue: "0 Warnings")
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

	public ReactiveCommand<Unit, Unit> ClearCommand { get; }

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
		ClearCommand.Dispose();
		GC.SuppressFinalize(this);
	}

	public void AddError(string message, string source)
	{
		PostOnUiThread(() =>
		{
			Entries.Add(new MessageEntry(MessageSeverity.Error, message, source, DateTime.Now));
			RecountAndNotify();
		});
	}

	public void AddWarning(string message, string source)
	{
		PostOnUiThread(() =>
		{
			Entries.Add(new MessageEntry(MessageSeverity.Warning, message, source, DateTime.Now));
			RecountAndNotify();
		});
	}

	public void AddInfo(string message, string source)
	{
		PostOnUiThread(() =>
		{
			Entries.Add(new MessageEntry(MessageSeverity.Info, message, source, DateTime.Now));
			RecountAndNotify();
		});
	}

	// Only IError and Warning subtypes are rendered; other IReason subtypes (plain Success) are intentionally ignored.
	public void RefreshReasons(IEnumerable<IReason> reasons)
	{
		ArgumentNullException.ThrowIfNull(reasons);
		var reasonList = reasons.ToList();
		PostOnUiThread(() =>
		{
			RemoveByPredicate(entry => entry.IsStructural);

			foreach (var error in reasonList.OfType<IError>())
			{
				Entries.Add(new MessageEntry(MessageSeverity.Error, error.Message, MessageEntry.StructuralSource,
					DateTime.Now));
			}

			foreach (var warning in reasonList.OfType<Warning>())
			{
				Entries.Add(new MessageEntry(MessageSeverity.Warning, warning.Message, MessageEntry.StructuralSource,
					DateTime.Now));
			}

			RecountAndNotify();
		});
	}

	public void Clear()
	{
		PostOnUiThread(() =>
		{
			Entries.Clear();
			ErrorCount = 0;
			WarningCount = 0;
			HasEntries = false;
		});
	}

	private void ClearNonStructural()
	{
		PostOnUiThread(() =>
		{
			RemoveByPredicate(entry => !entry.IsStructural);
			RecountAndNotify();
		});
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

	private void RemoveByPredicate(Func<MessageEntry, bool> predicate)
	{
		for (var i = Entries.Count - 1; i >= 0; i--)
		{
			if (predicate(Entries[i]))
			{
				Entries.RemoveAt(i);
			}
		}
	}

	private void RecountAndNotify()
	{
		ErrorCount = Entries.Count(e => e.IsError);
		WarningCount = Entries.Count(e => e.IsWarning);
		HasEntries = Entries.Count > 0;
	}
}
