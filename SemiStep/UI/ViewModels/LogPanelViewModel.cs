using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive;

using ReactiveUI;

using Shared.Reasons;

using UI.Models;

namespace UI.ViewModels;

public class LogPanelViewModel : ReactiveObject
{
	private bool _isVisible = true;
	private bool _suppressNotifications;
	private int _errorCount;
	private int _warningCount;

	public LogPanelViewModel()
	{
		Entries = new ObservableCollection<LogEntry>();
		Entries.CollectionChanged += OnEntriesChanged;

		ClearCommand = ReactiveCommand.Create(ClearNonStructural);
		ToggleCommand = ReactiveCommand.Create(Toggle);
	}

	public ObservableCollection<LogEntry> Entries { get; }

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

	public bool HasEntries => Entries.Count > 0;

	public bool HasErrors => ErrorCount > 0;

	public bool HasWarnings => WarningCount > 0;

	public string ErrorCountText => $"{ErrorCount} {(ErrorCount == 1 ? "Error" : "Errors")}";

	public string WarningCountText => $"{WarningCount} {(WarningCount == 1 ? "Warning" : "Warnings")}";

	public string StatusErrorSummary
	{
		get
		{
			var parts = new List<string>();
			if (ErrorCount > 0)
			{
				parts.Add(ErrorCountText);
			}

			if (WarningCount > 0)
			{
				parts.Add(WarningCountText);
			}

			return parts.Count > 0 ? string.Join(", ", parts) : string.Empty;
		}
	}

	public bool HasStatusErrors => ErrorCount > 0 || WarningCount > 0;

	public bool IsVisible
	{
		get => _isVisible;
		set
		{
			this.RaiseAndSetIfChanged(ref _isVisible, value);
			this.RaisePropertyChanged(nameof(ShowPanel));
		}
	}

	public bool ShowPanel => HasEntries && IsVisible;

	public void RefreshReasons(IReadOnlyList<AbstractReason> reasons)
	{
		_suppressNotifications = true;

		for (var i = Entries.Count - 1; i >= 0; i--)
		{
			var entry = Entries[i];
			if (entry.IsStructural)
			{
				AdjustCountersForRemoval(entry);
				Entries.RemoveAt(i);
			}
		}

		foreach (var reason in reasons)
		{
			var severity = reason is AbstractError ? LogSeverity.Error : LogSeverity.Warning;
			var entry = new LogEntry(severity, reason.Message, LogEntry.StructuralSource, DateTime.Now);
			AdjustCountersForAddition(entry);
			Entries.Add(entry);
		}

		_suppressNotifications = false;
		RaiseAllChanged();
	}

	public void Clear()
	{
		_suppressNotifications = true;
		Entries.Clear();
		ErrorCount = 0;
		WarningCount = 0;
		_suppressNotifications = false;
		RaiseAllChanged();
	}

	private void ClearNonStructural()
	{
		_suppressNotifications = true;

		for (var i = Entries.Count - 1; i >= 0; i--)
		{
			var entry = Entries[i];
			if (!entry.IsStructural)
			{
				AdjustCountersForRemoval(entry);
				Entries.RemoveAt(i);
			}
		}

		_suppressNotifications = false;
		RaiseAllChanged();
	}

	private void Toggle()
	{
		IsVisible = !IsVisible;
	}

	private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (_suppressNotifications)
		{
			return;
		}

		if (e.NewItems is not null)
		{
			foreach (LogEntry entry in e.NewItems)
			{
				AdjustCountersForAddition(entry);
			}
		}

		if (e.OldItems is not null)
		{
			foreach (LogEntry entry in e.OldItems)
			{
				AdjustCountersForRemoval(entry);
			}
		}

		if (e.Action == NotifyCollectionChangedAction.Reset)
		{
			ErrorCount = 0;
			WarningCount = 0;
		}

		RaiseAllChanged();
	}

	private void AdjustCountersForAddition(LogEntry entry)
	{
		if (entry.Severity == LogSeverity.Error)
		{
			ErrorCount++;
		}
		else if (entry.Severity == LogSeverity.Warning)
		{
			WarningCount++;
		}
	}

	private void AdjustCountersForRemoval(LogEntry entry)
	{
		if (entry.Severity == LogSeverity.Error)
		{
			ErrorCount = Math.Max(0, ErrorCount - 1);
		}
		else if (entry.Severity == LogSeverity.Warning)
		{
			WarningCount = Math.Max(0, WarningCount - 1);
		}
	}

	private void RaiseAllChanged()
	{
		this.RaisePropertyChanged(nameof(HasEntries));
		this.RaisePropertyChanged(nameof(ShowPanel));
		this.RaisePropertyChanged(nameof(HasErrors));
		this.RaisePropertyChanged(nameof(HasWarnings));
		this.RaisePropertyChanged(nameof(ErrorCountText));
		this.RaisePropertyChanged(nameof(WarningCountText));
		this.RaisePropertyChanged(nameof(StatusErrorSummary));
		this.RaisePropertyChanged(nameof(HasStatusErrors));
	}
}
