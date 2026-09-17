using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BankingThreads.Core;
using BankingThreads.UI;
using BankingThreads.UI.Controls;
using BankingThreads.UI.Presentation;
using BankingThreads.UI.Services;

internal static class CheckProgram
{
    [STAThread]
    private static int Main(string[] args)
    {
        string output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/ui-checks");
        Directory.CreateDirectory(output);
        AppBuilder.Configure<App>().UseSkia().WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var window = new MainWindow();
        window.Show();
        try
        {
            Pump();
            Check(window.Runs.Count == 0, "Initial screen contains no fabricated runs");
            Check(Control<Button>(window, "RunButton").IsEnabled, "Run control starts enabled");
            Check(Control<Button>(window, "ExportButton").IsEnabled == false, "Export requires real results");
            Capture(window, output, "01-ready.png");

            Click(window, "RunButton");
            Check(window.IsRunning && !Control<Button>(window, "RunButton").IsEnabled, "Run disables concurrent starts");
            Check(!Control<Button>(window, "UnsynchronizedButton").IsEnabled, "Mode is stable during a run");
            Click(window, "RunButton"); // Raised events also exercise the explicit re-entry guard.
            WaitUntil(() => !window.IsRunning);
            Check(window.Runs.Count == 1, "A duplicate click cannot launch a second run");
            var sync = window.Runs[0].Result;
            ValidateEvents(sync, 20);
            Check(sync.Synchronized, "Default mode uses the lock");
            Check(sync.ActualBalance >= 1419.36m && sync.ActualBalance <= 1419.39m,
                "Protected result is within the valid rounded serial-order range");
            Check(Control<TextBlock>(window, "BalanceText").Text == Display.Money(sync.ActualBalance), "UI displays the actual final balance");
            Check(Control<BalanceChart>(window, "Chart").Samples.Count == 7, "Chart uses starting, five completion, and final samples");
            Check(Control<ItemsControl>(window, "ActivityItems").ItemCount == sync.Events.Count, "Progress rows are not duplicated by queued callbacks");
            Capture(window, output, "02-synchronized.png");

            double expandedChartWidth = Control<BalanceChart>(window, "Chart").Bounds.Width;
            Click(window, "SidebarToggle");
            Pump();
            Check(!Control<Border>(window, "Sidebar").IsVisible, "Sidebar can be hidden");
            Check(Control<BalanceChart>(window, "Chart").Bounds.Width > expandedChartWidth,
                "Hiding navigation gives its space to the results");
            Check(Control<TextBlock>(window, "BalanceText").Text == Display.Money(sync.ActualBalance),
                "Changing the layout preserves the displayed experiment");
            Capture(window, output, "08-sidebar-hidden.png");
            Click(window, "SidebarToggle");
            Pump();
            Check(Control<Border>(window, "Sidebar").IsVisible, "Sidebar can be restored from the toolbar");

            Click(window, "UnsynchronizedButton");
            Click(window, "RunButton");
            WaitUntil(() => !window.IsRunning);
            var unsync = window.Runs[0].Result;
            ValidateEvents(unsync, 15);
            Check(!unsync.Synchronized, "Mode switch reaches the unprotected controller");
            Check(unsync.StartingBalance == sync.StartingBalance, "Each run starts with a reset account");
            Capture(window, output, "03-unsynchronized.png");

            Click(window, "RepeatButton");
            WaitUntil(() => !window.IsRunning, 20);
            Check(window.Runs.Count == 7, "Repeat control completes five separate trials");
            Check(window.Runs.Select(x => x.Number).Distinct().Count() == 7, "History identifiers are unique");
            Click(window, "HistoryNav");
            Pump();
            Check(Control<StackPanel>(window, "HistoryPage").IsVisible, "History navigation works");
            Check(Control<ItemsControl>(window, "HistoryItems").ItemCount == 7, "History includes every completed run");
            Capture(window, output, "04-history.png");
            var viewButton = Control<ItemsControl>(window, "HistoryItems").GetVisualDescendants()
                .OfType<Button>().First();
            viewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Control<StackPanel>(window, "OverviewPage").IsVisible, "A history row opens its experiment");
            Check(Control<TextBlock>(window, "BalanceText").Text == window.Runs[0].Balance, "History restores the selected result");

            string csv = SessionExport.CreateCsv(window.Runs);
            string[] lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Check(lines.Length == 1 + window.Runs.Sum(r => 1 + r.Result.Events.Count), "CSV preserves every summary and event");
            Check(lines.Count(x => x.StartsWith("\"summary\"")) == 7, "CSV has one summary per run");
            Check(csv.Contains("\"Synchronized\"") && csv.Contains("\"Unsynchronized\""), "CSV contains both experiment modes");
            Check(csv.Contains(Environment.Version.ToString()), "CSV records runtime information");
            File.WriteAllText(Path.Combine(output, "session.csv"), csv);

            Click(window, "PriorityNav");
            Check(Control<StackPanel>(window, "PriorityPage").IsVisible, "Scheduling opens the priority controller interface");
            Check(!Control<Button>(window, "ExportButton").IsEnabled, "Priority export requires a priority result");
            CheckPriority(window, output);
            Capture(window, output, "05-scheduling.png");
            Click(window, "GuideNav");
            Check(Control<StackPanel>(window, "GuidePage").IsVisible, "Presentation guide navigation works");
            Capture(window, output, "06-guide.png");
            Click(window, "OverviewNav");
            window.Width = 1080;
            window.Height = 740;
            Pump();
            Check(Control<Button>(window, "RunButton").Bounds.Width > 0, "Controls remain laid out at minimum window size");
            Capture(window, output, "07-compact.png");
            Click(window, "PriorityNav");
            Capture(window, output, "09-priority-compact.png");

            var closingWindow = new MainWindow();
            closingWindow.Show();
            Click(closingWindow, "RepeatButton");
            closingWindow.Close();
            Check(closingWindow.IsVisible, "Closing waits for the active workers");
            WaitUntil(() => !closingWindow.IsVisible);
            Check(closingWindow.Runs.Count == 1, "Closing ends a batch after its current experiment");

            var closingPriorityWindow = new MainWindow();
            closingPriorityWindow.Show();
            Click(closingPriorityWindow, "PriorityRunButton");
            closingPriorityWindow.Close();
            Check(closingPriorityWindow.IsVisible, "Closing waits for the active priority controller");
            WaitUntil(() => !closingPriorityWindow.IsVisible);
            Check(closingPriorityWindow.PriorityRuns.Count == 1, "Closing records the stopped priority run after joining its workers");

            Console.WriteLine($"PASS: all integration checks. Screenshots and CSV: {output}");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally { window.Close(); }
    }

    private static void CheckPriority(MainWindow window, string output)
    {
        var originalOutput = Console.Out;
        int normalRunCount = window.Runs.Count;
        var workers = Control<ItemsControl>(window, "PriorityWorkerItems").ItemsSource!.Cast<PriorityWorkerModel>().ToArray();
        Check(workers.Select(x => x.Definition.Method).SequenceEqual(new[] {
            "VerifySolvencyAndNonce", "CalculateLoanAmortization", "LowPriorityTransactions" }),
            "Scheduling lists exactly the three priority-controller workloads");
        Click(window, "PriorityModeButton");
        Check(workers.All(x => x.Priority == "Normal"), "Priority off previews Normal for all ready methods");
        Click(window, "PriorityModeButton");
        Click(window, "PriorityRunButton");
        Check(window.IsRunning && !Control<Button>(window, "RunButton").IsEnabled,
            "Priority execution prevents overlapping UI runs");
        Check(Control<Button>(window, "PriorityStopButton").IsVisible, "Long-running priority work has a Stop control");
        Check(!Control<Button>(window, "PriorityModeButton").IsEnabled, "Priority cannot change during a run");
        Click(window, "PriorityModeButton");
        Click(window, "RunButton");
        Click(window, "PriorityRunButton");
        WaitUntil(() => workers[0].Latest?.Phase == PriorityPhase.Completed
            && workers[1].Latest?.Phase == PriorityPhase.Completed
            && workers[2].Latest?.Phase == PriorityPhase.Running);
        Check(workers[0].Latest!.Result!.RemainingBalance == 4000m,
            "Real solvency method returns the remaining $4,000 balance");
        Check(Convert.FromHexString(workers[0].Latest!.Result!.TransactionHash!).Length == 32,
            "Real solvency method returns a SHA-256 hash");
        Check(workers[1].Latest!.Result!.Iterations == 10000,
            "Real amortization method returns 10,000 iterations");
        Check(workers[2].Latest!.Result == null && window.IsRunning,
            "Long transaction method remains running without a fabricated result");
        Capture(window, output, "10-priority-running.png");
        Click(window, "PriorityStopButton");
        WaitUntil(() => !window.IsRunning);
        Check(window.PriorityRuns.Count == 1 && window.Runs.Count == normalRunCount,
            "Duplicate and cross-mode clicks cannot launch additional experiments");
        Check(ReferenceEquals(originalOutput, Console.Out), "Priority workloads do not redirect console output");
        var result = window.PriorityRuns[0].Result;
        Check(result.UseAssignedPriorities, "Default run preserves assigned priorities despite a mid-run toggle attempt");
        Check(result.Completed == 2 && result.Status == "Stopped", "Stop preserves finished results and records a stopped run");
        Check(result.Events.Count == 6 && result.Events.Select(x => x.ThreadId).Distinct().Count() == 3,
            "Three real worker threads each report a start and terminal state");
        Check(result.Events.Select(x => x.Sequence).SequenceEqual(Enumerable.Range(1, 6)),
            "Priority events retain a consistent reported sequence");
        Check(result.Events.All(x => x.Elapsed <= result.Duration), "Priority event times fall within the run");
        foreach (var definition in PriorityWorkerModel.Definitions)
            Check(result.Events.Where(x => x.Method == definition.Method).All(x => x.Priority == definition.Priority),
                $"Worker uses {definition.Priority} for {definition.Method}");
        var stopped = result.Events.Single(x => x.Phase == PriorityPhase.Stopped);
        Check(stopped.Method == "LowPriorityTransactions" && stopped.Result == null,
            "Stop interrupts the sleeping transaction worker without inventing partial results");
        Check(Control<TextBlock>(window, "PriorityCompletedText").Text == "2 / 3",
            "UI distinguishes completed methods from stopped work");
        Check(Control<Button>(window, "ExportButton").IsEnabled, "Partial priority results can be exported");

        Click(window, "PriorityModeButton");
        Check(Control<Button>(window, "PriorityModeButton").Content?.ToString() == "Priority: Off",
            "Priority can be switched off for the next run");
        Check(workers[0].Priority == "Highest", "Changing the next run does not relabel past results");
        Click(window, "PriorityRunButton");
        WaitUntil(() => workers.All(x => x.Latest != null) && workers[0].Latest?.Phase == PriorityPhase.Completed
            && workers[1].Latest?.Phase == PriorityPhase.Completed);
        Click(window, "PriorityStopButton");
        WaitUntil(() => !window.IsRunning);
        Check(window.PriorityRuns.Count == 2, "Priority runs can restart after stopping and joining");
        Check(!window.PriorityRuns[0].Result.UseAssignedPriorities
            && window.PriorityRuns[0].Result.Events.All(x => x.Priority == ThreadPriority.Normal),
            "Priority off runs all three real methods on threads reporting Normal priority");
        Capture(window, output, "11-priority-off.png");
        Pump();
        var historyButton = Control<ItemsControl>(window, "PriorityHistoryItems").GetVisualDescendants()
            .OfType<Button>().Last();
        historyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(Control<TextBlock>(window, "PriorityRunSummary").Text == "Run 1 · Priority on · Stopped",
            "History restores the original stopped state");
        Check(Control<Button>(window, "PriorityModeButton").Content?.ToString() == "Priority: On"
            && workers[0].Priority == "Highest", "History restores the recorded priority mode and values");
        Check(workers[0].Latest?.Result?.TransactionHash == result.Events.Single(x => x.Method == "VerifySolvencyAndNonce"
            && x.Phase == PriorityPhase.Completed).Result?.TransactionHash,
            "History restores the actual recorded hash instead of rerunning the method");
        string csv = PriorityExport.CreateCsv(window.PriorityRuns);
        Check(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == 1 + window.PriorityRuns.Sum(x => 1 + x.Result.Events.Count),
            "CSV preserves summaries and every actual priority event");
        Check(csv.Contains("4000.00") && csv.Contains("10000") && csv.Contains("Stopped") && csv.Contains("transaction_hash"),
            "CSV includes method return values and explicit stopped states");
        Check(csv.Contains("priority_mode") && csv.Contains("Priority on") && csv.Contains("Priority off"),
            "CSV distinguishes runs with priority on and off");
        File.WriteAllText(Path.Combine(output, "priority-session.csv"), csv);

        // Real long-workload start/stop was tested above. Short fixtures exercise complete
        // and failure paths without replacing or shortening the application's 17-minute method.
        var shortJobs = PriorityRunner.Workloads.Select(work => work.InterruptibleSleep
            ? work with { Execute = () => new PriorityWorkResult(Iterations: 7), InterruptibleSleep = false }
            : work).ToArray();
        var complete = PriorityRunner.Run(shortJobs, null, CancellationToken.None);
        Check(complete.Completed == 3 && complete.Status == "Finished", "Executor records completion for all short fixtures");
        var normal = PriorityRunner.Run(shortJobs, null, CancellationToken.None, useAssignedPriorities: false);
        Check(normal.Completed == 3 && !normal.UseAssignedPriorities
            && normal.Events.All(x => x.Priority == ThreadPriority.Normal),
            "Equal-priority execution also records successful completion");
        var failedJobs = shortJobs.Select(work => work.Definition.Method == "CalculateLoanAmortization"
            ? work with { Execute = () => throw new InvalidOperationException("Test worker failure") } : work).ToArray();
        var failed = PriorityRunner.Run(failedJobs, null, CancellationToken.None);
        Check(failed.Status == "Failed" && failed.Events.Any(x => x.Error == "Test worker failure"),
            "Worker exceptions become visible failures instead of terminating the process");
        bool callbackFailureReported = false;
        try { PriorityRunner.Run(shortJobs, _ => throw new InvalidOperationException("Test display failure"), CancellationToken.None); }
        catch (InvalidOperationException error) { callbackFailureReported = error.InnerException?.Message == "Test display failure"; }
        Check(callbackFailureReported, "Display failures are reported after workers have joined");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var earlyStop = PriorityRunner.Run(null, cancelled.Token);
        Check(earlyStop.Events.Count == 3 && earlyStop.Events.All(x => x.Phase == PriorityPhase.Stopped),
            "Cancellation before startup skips all original workloads safely");
    }

    private static T Control<T>(MainWindow window, string name) where T : Control =>
        window.FindControl<T>(name) ?? throw new Exception($"Missing control: {name}");
    private static void Click(MainWindow window, string name) =>
        Control<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
    private static void WaitUntil(Func<bool> condition, int seconds = 12)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            Pump();
            if (timer.Elapsed.TotalSeconds > seconds) throw new TimeoutException("UI operation did not finish.");
            Thread.Sleep(10);
        }
        Pump();
    }
    private static void Capture(MainWindow window, string folder, string filename)
    {
        Pump();
        using var frame = window.CaptureRenderedFrame() ?? throw new Exception("No rendered frame.");
        frame.Save(Path.Combine(folder, filename), PngBitmapEncoderOptions.Default);
    }
    private static void ValidateEvents(SimulationResult result, int expectedCount)
    {
        Check(result.ReferenceBalance == 1419.37m, "Reference uses the existing calculations");
        Check(result.Events.Count == expectedCount, "Expected worker lifecycle events are present");
        Check(result.Events.Select(x => x.Sequence).SequenceEqual(Enumerable.Range(1, expectedCount)), "Reported event ordering is stable");
        Check(result.Events.Where(x => x.Update.Phase == WorkerPhase.Started).Select(x => x.Update.WorkerId).Distinct().Count() == 5, "Five distinct workers start");
        Check(result.Events.Count(x => x.Update.Phase == WorkerPhase.Completed) == 5, "All workers complete before the result is returned");
    }
}
