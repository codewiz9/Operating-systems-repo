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
            StatusText.Text = "Finishing this run before closing…";
            StatusText.IsVisible = true;
        };
    }

    private void OnToggleSidebarClick(object? sender, RoutedEventArgs e)
    {
        Sidebar.IsVisible = !Sidebar.IsVisible;
        WindowLayout.ColumnDefinitions[0].Width = new GridLength(Sidebar.IsVisible ? 188 : 0);
        string label = Sidebar.IsVisible ? "Hide sidebar" : "Show sidebar";
        ToolTip.SetTip(SidebarToggle, label);
        Avalonia.Automation.AutomationProperties.SetName(SidebarToggle, label);
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
        BreadcrumbText.Text = page switch { "history" => "History", "priority" => "Scheduling", "guide" => "Notes", _ => "Overview" };
    }

    private void SetMode(bool synchronized)
    {
        _synchronized = synchronized;
        SynchronizedButton.Classes.Set("selected", synchronized);
        UnsynchronizedButton.Classes.Set("selected", !synchronized);
    }

    private void SetRunning(bool running)
    {
        _running = running;
        RunButton.IsEnabled = RepeatButton.IsEnabled = !running;
        SynchronizedButton.IsEnabled = UnsynchronizedButton.IsEnabled = !running;
        HistoryItems.IsEnabled = !running;
        RunButtonText.Text = running ? "Running…" : "Run";
        if (running) StatusText.IsVisible = false;
    }

    private void ResetDashboard(int number)
    {
        _events.Clear();
        foreach (var worker in _workers) worker.SetPhase(null);
        EmptyActivity.IsVisible = true;
        Chart.Reset(SimulationApi.StartingBalance);
        BalanceText.Text = Display.Money(SimulationApi.StartingBalance);
        BalanceCaption.Text = "Balance";
        ReferenceText.Text = DifferenceText.Text = DurationText.Text = "—";
        DifferenceText.Foreground = Display.Brush("#222631");
        RunSummaryText.Text = $"Run {number} · {(_synchronized ? "Synchronized" : "Unsynchronized")}";
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
                if (count > 1) RunButtonText.Text = $"{trial}/{count}…";
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
            StatusText.Text = $"Run failed: {cause.Message}";
            StatusText.IsVisible = true;
            RunSummaryText.Text = "Run failed";
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
        _workers.First(x => x.Id == entry.Update.WorkerId).SetPhase(entry.Update.Phase);
        if (entry.Update.ObservedBalance is decimal balance)
        {
            Chart.AddSample(entry.Elapsed.TotalMilliseconds, balance);
            BalanceText.Text = Display.Money(balance);
            BalanceCaption.Text = "Observed balance";
        }
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
        RunSummaryText.Text = $"Run {record.Number} · {record.Mode}";
        ActivityScroll.ScrollToHome();
    }

    private void UpdateHistory()
    {
        EmptyHistory.IsVisible = _history.Count == 0;
        ExportButton.IsEnabled = _history.Count > 0;
        SessionSummary.Text = $"{_history.Count} {(_history.Count == 1 ? "run" : "runs")}";
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
                Title = "Export CSV",
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
            NoticeText.Text = "CSV saved";
        }
        catch (Exception error)
        {
            NoticeText.Text = "Export failed";
            ToolTip.SetTip(NoticeText, error.Message);
        }
    }
}
