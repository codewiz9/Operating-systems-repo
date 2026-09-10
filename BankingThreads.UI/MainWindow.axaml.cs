using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using BankingThreads.Core;
using BankingThreads.UI.Presentation;

namespace BankingThreads.UI;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<WorkerModel> _workers = new();
    private readonly ObservableCollection<EventModel> _events = new();
    private readonly ObservableCollection<RunRecord> _history = new();
    private bool _synchronized = true;
    private bool _running;
    private bool _closeAfterRun;
    private int _displayGeneration;

    public bool IsRunning => _running;
    public IReadOnlyList<RunRecord> Runs => _history;

    public MainWindow()
    {
        InitializeComponent();
        foreach (var worker in WorkerModel.Definitions)
            _workers.Add(new WorkerModel(worker.Id, worker.Title, worker.Rate, worker.Symbol, worker.Color));
        WorkerItems.ItemsSource = _workers;
        ActivityItems.ItemsSource = _events;
        HistoryItems.ItemsSource = _history;
        BalanceText.Text = InitialBalanceText.Text = Display.Money(SimulationApi.StartingBalance);
        Chart.Reset(SimulationApi.StartingBalance);
        PlatformText.Text = $"{(OperatingSystem.IsMacOS() ? "macOS" : OperatingSystem.IsWindows() ? "Windows" : "Linux")} · .NET {Environment.Version.Major}";
        Closing += (_, e) =>
        {
            if (!_running) return;
            e.Cancel = true;
            _closeAfterRun = true;
            StatusText.Text = "Finishing the current experiment before closing…";
        };
    }

    private void OnOverviewClick(object? sender, RoutedEventArgs e) => ShowPage("overview");
    private void OnHistoryClick(object? sender, RoutedEventArgs e) => ShowPage("history");
    private void OnPriorityClick(object? sender, RoutedEventArgs e) => ShowPage("priority");
    private void OnGuideClick(object? sender, RoutedEventArgs e) => ShowPage("guide");
    private void OnSynchronizedClick(object? sender, RoutedEventArgs e) { if (!_running) SetMode(true); }
    private void OnUnsynchronizedClick(object? sender, RoutedEventArgs e) { if (!_running) SetMode(false); }
    private async void OnRunClick(object? sender, RoutedEventArgs e) => await RunExperimentsAsync(1);
    private async void OnRepeatClick(object? sender, RoutedEventArgs e) => await RunExperimentsAsync(5);

    private void ShowPage(string page)
    {
        OverviewPage.IsVisible = page == "overview";
        HistoryPage.IsVisible = page == "history";
        PriorityPage.IsVisible = page == "priority";
        GuidePage.IsVisible = page == "guide";
        OverviewNav.Classes.Set("selected", page == "overview");
        HistoryNav.Classes.Set("selected", page == "history");
        PriorityNav.Classes.Set("selected", page == "priority");
        GuideNav.Classes.Set("selected", page == "guide");
        BreadcrumbText.Text = page switch { "history" => "Run history", "priority" => "Scheduling", "guide" => "How it works", _ => "Overview" };
    }

    private void SetMode(bool synchronized)
    {
        _synchronized = synchronized;
        SynchronizedButton.Classes.Set("selected", synchronized);
        UnsynchronizedButton.Classes.Set("selected", !synchronized);
        ModeDescription.Text = synchronized
            ? "One worker holds the account lock at a time. Execution order can still vary."
            : "All five workers update the same account without a lock. Updates can overwrite one another.";
    }

    private void SetRunning(bool running)
    {
        _running = running;
        RunButton.IsEnabled = RepeatButton.IsEnabled = !running;
        SynchronizedButton.IsEnabled = UnsynchronizedButton.IsEnabled = !running;
        HistoryItems.IsEnabled = !running;
        RunProgress.IsVisible = running;
        RunButtonText.Text = running ? "Running…" : "Run simulation";
        ActivityState.Text = running ? "LIVE" : "COMPLETE";
        LiveDot.Fill = Display.Brush(running ? "#20A38B" : "#BCC3D0");
    }

    private void ResetDashboard(int number)
    {
        _events.Clear();
        foreach (var worker in _workers) worker.SetPhase(null);
        EmptyActivity.IsVisible = true;
        EventCountText.Text = "0 events";
        Chart.Reset(SimulationApi.StartingBalance);
        BalanceText.Text = Display.Money(SimulationApi.StartingBalance);
        BalanceCaption.Text = "Starting balance";
        BalanceDescription.Text = "Preparing a fresh account for this experiment.";
        ReferenceText.Text = DifferenceText.Text = DurationText.Text = "—";
        DifferenceText.Foreground = Display.Brush("#222631");
        RunBadgeText.Text = $"Run {number:00} · {(_synchronized ? "Synchronized" : "Unsynchronized")}";
        RunBadge.Background = Display.Brush(_synchronized ? "#EDF3FF" : "#FBF3E6");
        StatusText.Text = "Calculating the sequential reference…";
    }

    private async Task RunExperimentsAsync(int count)
    {
        if (_running) return;
        SetRunning(true);
        ShowPage("overview");
        try
        {
            for (int trial = 1; trial <= count; trial++)
            {
                int number = _history.Count + 1;
                int generation = ++_displayGeneration;
                ResetDashboard(number);
                if (count > 1) RunButtonText.Text = $"Trial {trial} of {count}…";
                bool mode = _synchronized;

                // Join() blocks the coordinator, not the UI. The original five threads still do the work.
                var result = await Task.Run(() => SimulationApi.Run(mode, entry =>
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (generation == _displayGeneration) ApplyEvent(entry);
                    })));

                // Ignore late progress from the completed run and use its authoritative event list.
                ++_displayGeneration;
                var record = new RunRecord(number, DateTimeOffset.Now, result);
                _history.Insert(0, record);
                UpdateHistory();
                ShowResult(record);
                if (_closeAfterRun) break;
            }
        }
        catch (Exception error)
        {
            ++_displayGeneration;
            foreach (var worker in _workers.Where(x => x.Phase != WorkerPhase.Completed))
                worker.SetPhase(WorkerPhase.Failed);
            var cause = error is AggregateException aggregate ? aggregate.Flatten().InnerExceptions[0] : error;
            StatusText.Text = $"Experiment could not complete: {cause.Message}";
            BalanceDescription.Text = "This run was not added to history. You can try again.";
            RunBadgeText.Text = "Run interrupted";
        }
        finally
        {
            SetRunning(false);
            if (_closeAfterRun) Close();
        }
    }

    private void ApplyEvent(SimulationEvent entry)
    {
        _events.Add(new EventModel(entry));
        EmptyActivity.IsVisible = false;
        EventCountText.Text = $"{_events.Count} events";
        _workers.First(x => x.Id == entry.Update.WorkerId).SetPhase(entry.Update.Phase);
        if (entry.Update.ObservedBalance is decimal balance)
        {
            Chart.AddSample(entry.Elapsed.TotalMilliseconds, balance);
            BalanceText.Text = Display.Money(balance);
            BalanceCaption.Text = "Latest observed balance";
        }
        int completed = _workers.Count(x => x.Phase == WorkerPhase.Completed);
        StatusText.Text = $"{completed} of 5 workers completed · events shown in reported order";
        ActivityScroll.ScrollToEnd();
    }

    private void ShowResult(RunRecord record)
    {
        var result = record.Result;
        _events.Clear();
        foreach (var worker in _workers) worker.SetPhase(null);
        Chart.Reset(result.StartingBalance);
        foreach (var entry in result.Events) ApplyEvent(entry);
        Chart.Complete(result.ReferenceBalance, result.Duration.TotalMilliseconds, result.ActualBalance);
        BalanceCaption.Text = "Final balance";
        BalanceText.Text = Display.Money(result.ActualBalance);
        InitialBalanceText.Text = Display.Money(result.StartingBalance);
        ReferenceText.Text = Display.Money(result.ReferenceBalance);
        DifferenceText.Text = Display.Difference(result.ActualBalance - result.ReferenceBalance);
        DifferenceText.Foreground = Display.Brush("#64708A");
        DurationText.Text = record.Duration;
        BalanceDescription.Text = result.Synchronized ? "All five workers finished with mutual exclusion." : "All five workers finished without the account lock.";
        RunBadgeText.Text = $"Run {record.Number:00} · {record.Mode}";
        RunBadge.Background = Display.Brush(result.Synchronized ? "#EDF3FF" : "#FBF3E6");
        StatusText.Text = $"Run {record.Number:00} complete · 5 workers · 20 updates · {record.Duration}";
        ActivityState.Text = "COMPLETE";
        ActivityScroll.ScrollToHome();
    }

    private void UpdateHistory()
    {
        HistoryCount.Text = _history.Count.ToString();
        EmptyHistory.IsVisible = _history.Count == 0;
        ExportButton.IsEnabled = _history.Count > 0;
        int synchronized = _history.Count(x => x.Result.Synchronized);
        SessionSummary.Text = $"{_history.Count} runs · {synchronized} synchronized · {_history.Count - synchronized} unsynchronized";
    }

    private void OnViewRunClick(object? sender, RoutedEventArgs e)
    {
        if (_running || sender is not Button { DataContext: RunRecord record }) return;
        ++_displayGeneration;
        SetMode(record.Result.Synchronized);
        ShowResult(record);
        ShowPage("overview");
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (_history.Count == 0) return;
        var snapshot = _history.ToArray();
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Ledger session",
                SuggestedFileName = $"ledger-session-{DateTime.Now:yyyyMMdd-HHmm}.csv",
                DefaultExtension = "csv",
                FileTypeChoices = new[] { new FilePickerFileType("CSV spreadsheet") { Patterns = new[] { "*.csv" } } },
                ShowOverwritePrompt = true
            });
            if (file == null) return;
            using (file)
            {
                await using var stream = await file.OpenWriteAsync();
                if (stream.CanSeek) stream.SetLength(0);
                await using var writer = new StreamWriter(stream, new UTF8Encoding(true));
                await writer.WriteAsync(SessionExport.CreateCsv(snapshot));
            }
            NoticeText.Text = $"Exported {snapshot.Length} runs";
        }
        catch (Exception error)
        {
            NoticeText.Text = "Export did not complete";
            ToolTip.SetTip(NoticeText, error.Message);
        }
    }
}
