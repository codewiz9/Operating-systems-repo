using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Avalonia.Media;

namespace BankingThreads.UI.Presentation;

public enum PriorityPhase { Running, Completed, Stopped, Failed }

public sealed record PriorityWorkerDefinition(string Method, string Title, string Description, ThreadPriority Priority);
public sealed record PriorityWorkResult(decimal? RemainingBalance = null, string? TransactionHash = null,
    int? Iterations = null)
{
    public string Summary => RemainingBalance is decimal balance ? $"{Display.Money(balance)} remaining"
        : Iterations is int iterations ? $"{iterations:N0} iterations" : "—";
    public string Detail => TransactionHash == null ? string.Empty : $"SHA-256: {TransactionHash}";
}

public sealed record PriorityUpdate(int Sequence, string Method, ThreadPriority Priority, int ThreadId,
    PriorityPhase Phase, TimeSpan Elapsed, TimeSpan Duration, PriorityWorkResult? Result = null, string? Error = null);

public sealed record PriorityRunResult(TimeSpan Duration, IReadOnlyList<PriorityUpdate> Events,
    bool UseAssignedPriorities = true)
{
    public string Mode => UseAssignedPriorities ? "Priority on" : "Priority off";
    public int Completed => Events.Count(x => x.Phase == PriorityPhase.Completed);
    public string Status => Events.Any(x => x.Phase == PriorityPhase.Failed) ? "Failed"
        : Events.Any(x => x.Phase == PriorityPhase.Stopped) ? "Stopped" : "Finished";
}

public sealed record PriorityRunRecord(int Number, DateTimeOffset RecordedAt, PriorityRunResult Result)
{
    public string NumberLabel => Number.ToString("00");
    public string Time => RecordedAt.ToLocalTime().ToString("h:mm:ss tt");
    public string Mode => Result.Mode;
    public string Outcome => $"{Result.Completed}/3 completed · {Result.Status}";
    public string Duration => Result.Duration.TotalSeconds >= 1 ? $"{Result.Duration.TotalSeconds:0.0} s"
        : $"{Result.Duration.TotalMilliseconds:0.000} ms";
}

public sealed class PriorityWorkerModel : INotifyPropertyChanged
{
    public static IReadOnlyList<PriorityWorkerDefinition> Definitions { get; } = Array.AsReadOnly(new[]
    {
        new PriorityWorkerDefinition("VerifySolvencyAndNonce", "Solvency & nonce", "Verifies a withdrawal and generates a SHA-256 transaction hash.", ThreadPriority.Highest),
        new PriorityWorkerDefinition("CalculateLoanAmortization", "Loan amortization", "Performs 10,000 loan-payment calculations; returns the iteration count.", ThreadPriority.Normal),
        new PriorityWorkerDefinition("LowPriorityTransactions", "Background transactions", "Processes 100,000 random transactions with a 10 ms pause each.", ThreadPriority.Lowest)
    });

    public PriorityWorkerDefinition Definition { get; }
    public string Title => Definition.Title;
    public string Method => Definition.Method + "()";
    public string Description => Definition.Description;
    private bool _useAssignedPriorities = true;
    public string Priority => (Latest?.Priority ?? (_useAssignedPriorities ? Definition.Priority : ThreadPriority.Normal)).ToString();
    public PriorityUpdate? Latest { get; private set; }
    public string Start { get; private set; } = "—";
    public string Duration => Latest == null || Latest.Phase == PriorityPhase.Running ? "—"
        : Latest.Duration.TotalSeconds >= 1 ? $"{Latest.Duration.TotalSeconds:0.0} s"
        : $"{Latest.Duration.TotalMilliseconds:0.000} ms";
    public string Status => Latest?.Phase switch
    {
        PriorityPhase.Running => "Running",
        PriorityPhase.Completed => "Finished",
        PriorityPhase.Stopped => "Stopped",
        PriorityPhase.Failed => "Failed",
        _ => "Ready"
    };
    public string Result => Latest?.Result?.Summary ?? (Latest?.Phase switch
    {
        PriorityPhase.Stopped => "Stopped before returning a result",
        PriorityPhase.Failed => Latest.Error ?? "Method failed",
        _ => string.Empty
    });
    public string Detail => Latest?.Result?.Detail ?? string.Empty;
    public bool HasResult => !string.IsNullOrEmpty(Result);
    public bool HasDetail => !string.IsNullOrEmpty(Detail);
    public IBrush StatusBrush => Display.Brush(Status switch
    {
        "Finished" => "#208663", "Running" => "#0069D9", "Failed" => "#C34F50", _ => "#6E6E73"
    });
    public event PropertyChangedEventHandler? PropertyChanged;
    public PriorityWorkerModel(PriorityWorkerDefinition definition) => Definition = definition;

    public void SetPriorityMode(bool useAssignedPriorities)
    {
        // Existing results keep their recorded priority; ready rows preview the next run.
        _useAssignedPriorities = useAssignedPriorities;
        Notify();
    }

    public void Reset()
    {
        Latest = null;
        Start = "—";
        Notify();
    }

    public void Apply(PriorityUpdate update)
    {
        Latest = update;
        if (update.Phase == PriorityPhase.Running) Start = $"{update.Elapsed.TotalMilliseconds:0.000} ms";
        Notify();
    }

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}

public static class PriorityExport
{
    public static string CreateCsv(IEnumerable<PriorityRunRecord> runs)
    {
        var output = new StringBuilder("row_type,run,recorded_at,run_status,run_duration_ms,priority_mode,event_sequence,event_ms,method,priority,managed_thread_id,phase,method_duration_ms,remaining_balance,transaction_hash,completed_iterations,error,os,dotnet\n");
        string Milliseconds(TimeSpan value) => value.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture);
        void Row(IEnumerable<string> values) => output.AppendLine(string.Join(",", values.Select(value =>
            "\"" + value.Replace("\"", "\"\"") + "\"")));
        foreach (var run in runs.OrderBy(x => x.Number))
        {
            string[] Prefix(string kind) => new[] { kind, run.Number.ToString(CultureInfo.InvariantCulture),
                run.RecordedAt.ToString("O"), run.Result.Status, Milliseconds(run.Result.Duration), run.Result.Mode };
            string[] environment = { Environment.OSVersion.ToString(), Environment.Version.ToString() };
            Row(Prefix("summary").Concat(Enumerable.Repeat(string.Empty, 11)).Concat(environment));
            foreach (var update in run.Result.Events)
                Row(Prefix("event").Concat(new[] { update.Sequence.ToString(CultureInfo.InvariantCulture),
                    Milliseconds(update.Elapsed), update.Method, update.Priority.ToString(),
                    update.ThreadId.ToString(CultureInfo.InvariantCulture), update.Phase.ToString(),
                    update.Phase == PriorityPhase.Running ? "" : Milliseconds(update.Duration),
                    update.Result?.RemainingBalance?.ToString("F2", CultureInfo.InvariantCulture) ?? "",
                    update.Result?.TransactionHash ?? "", update.Result?.Iterations?.ToString(CultureInfo.InvariantCulture) ?? "",
                    update.Error ?? "" }).Concat(environment));
        }
        return output.ToString();
    }
}
