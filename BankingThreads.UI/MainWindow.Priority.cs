using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BankingThreads.UI.Presentation;
using BankingThreads.UI.Services;

namespace BankingThreads.UI;

public partial class MainWindow
{
    private readonly ObservableCollection<PriorityWorkerModel> _priorityWorkers = new();
    private readonly ObservableCollection<PriorityRunRecord> _priorityHistory = new();
    private readonly DispatcherTimer _priorityTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly Stopwatch _priorityClock = new();
    private CancellationTokenSource? _priorityCancellation;
    private bool _priorityRunning;
    private bool _useAssignedPriorities = true;
    public IReadOnlyList<PriorityRunRecord> PriorityRuns => _priorityHistory;

    private void InitializePriority()
    {
        foreach (var definition in PriorityWorkerModel.Definitions)
            _priorityWorkers.Add(new PriorityWorkerModel(definition));
        PriorityWorkerItems.ItemsSource = _priorityWorkers;
        PriorityHistoryItems.ItemsSource = _priorityHistory;
        _priorityTimer.Tick += (_, _) => PriorityDurationText.Text = $"{_priorityClock.Elapsed.TotalSeconds:0.0} s";
    }

    private async void OnPriorityRunClick(object? sender, RoutedEventArgs e) => await RunPriorityAsync();
    private void OnPriorityStopClick(object? sender, RoutedEventArgs e) => StopPriority();

    private void OnPriorityModeClick(object? sender, RoutedEventArgs e)
    {
        if (_running) return;
        SetPriorityMode(!_useAssignedPriorities);
    }

    private void SetPriorityMode(bool useAssignedPriorities)
    {
        _useAssignedPriorities = useAssignedPriorities;
        PriorityModeButton.Content = useAssignedPriorities ? "Priority: On" : "Priority: Off";
        foreach (var worker in _priorityWorkers) worker.SetPriorityMode(useAssignedPriorities);
    }

    private void StopPriority()
    {
        if (!_priorityRunning) return;
        PriorityStopButton.IsEnabled = false;
        PriorityStopButton.Content = "Stopping…";
        _priorityCancellation?.Cancel();
    }

    private async Task RunPriorityAsync()
    {
        if (_running) return;
        bool useAssignedPriorities = _useAssignedPriorities;
        using var cancellation = new CancellationTokenSource();
        _priorityCancellation = cancellation;
        _priorityRunning = true;
        SetRunning(true);
        ShowPage("priority");
        int number = _priorityHistory.Count + 1;
        int generation = ++_displayGeneration;
        foreach (var worker in _priorityWorkers) worker.Reset();
        PriorityDurationText.Text = "0.0 s";
        PriorityCompletedText.Text = "0 / 3";
        PriorityRunSummary.Text = $"Run {number} · Priority {(useAssignedPriorities ? "on" : "off")} · Running";
        PriorityStopButton.Content = "Stop";
        _priorityClock.Restart();
        _priorityTimer.Start();
        try
        {
            var result = await Task.Run(() => PriorityRunner.Run(update =>
                Dispatcher.UIThread.Post(() =>
                {
                    if (generation == _displayGeneration) ApplyPriorityUpdate(update);
                }), cancellation.Token, useAssignedPriorities));

            ++_displayGeneration;
            var record = new PriorityRunRecord(number, DateTimeOffset.Now, result);
            _priorityHistory.Insert(0, record);
            ShowPriorityResult(record);
            PriorityHistorySection.IsVisible = true;
            UpdateExportState();
        }
        catch (Exception error)
        {
            ++_displayGeneration;
            PriorityRunSummary.Text = "Run failed";
            PriorityStatusText.Text = error.Message;
            PriorityStatusText.IsVisible = true;
        }
        finally
        {
            _priorityTimer.Stop();
            _priorityClock.Stop();
            _priorityCancellation = null;
            _priorityRunning = false;
            SetRunning(false);
            if (_closeAfterRun) Close();
        }
    }

    private void ApplyPriorityUpdate(PriorityUpdate update)
    {
        _priorityWorkers.First(worker => worker.Definition.Method == update.Method).Apply(update);
        PriorityCompletedText.Text = $"{_priorityWorkers.Count(worker => worker.Latest?.Phase == PriorityPhase.Completed)} / 3";
    }

    private void ShowPriorityResult(PriorityRunRecord record)
    {
        SetPriorityMode(record.Result.UseAssignedPriorities);
        foreach (var worker in _priorityWorkers) worker.Reset();
        foreach (var update in record.Result.Events) ApplyPriorityUpdate(update);
        PriorityDurationText.Text = record.Duration;
        PriorityRunSummary.Text = $"Run {record.Number} · {record.Mode} · {record.Result.Status}";
        PriorityStatusText.IsVisible = false;
    }

    private void OnViewPriorityRunClick(object? sender, RoutedEventArgs e)
    {
        if (_running || sender is not Button { DataContext: PriorityRunRecord record }) return;
        ++_displayGeneration;
        ShowPriorityResult(record);
    }

    private void UpdateExportState()
    {
        ExportButton.IsEnabled = PriorityPage.IsVisible ? _priorityHistory.Count > 0 : _history.Count > 0;
        ToolTip.SetTip(ExportButton, PriorityPage.IsVisible
            ? "Save priority method results, times, and states" : "Save this session's results and events");
    }
}
