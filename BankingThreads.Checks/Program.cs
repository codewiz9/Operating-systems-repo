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
            Check(Control<StackPanel>(window, "PriorityPage").IsVisible, "Scheduling explains the pending implementation");
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

            var closingWindow = new MainWindow();
            closingWindow.Show();
            Click(closingWindow, "RepeatButton");
            closingWindow.Close();
            Check(closingWindow.IsVisible, "Closing waits for the active workers");
            WaitUntil(() => !closingWindow.IsVisible);
            Check(closingWindow.Runs.Count == 1, "Closing ends a batch after its current experiment");

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
        Check(result.ReferenceBalance == 1419.38m, "Reference uses the existing calculations");
        Check(result.Events.Count == expectedCount, "Expected worker lifecycle events are present");
        Check(result.Events.Select(x => x.Sequence).SequenceEqual(Enumerable.Range(1, expectedCount)), "Reported event ordering is stable");
        Check(result.Events.Where(x => x.Update.Phase == WorkerPhase.Started).Select(x => x.Update.WorkerId).Distinct().Count() == 5, "Five distinct workers start");
        Check(result.Events.Count(x => x.Update.Phase == WorkerPhase.Completed) == 5, "All workers complete before the result is returned");
    }
}
