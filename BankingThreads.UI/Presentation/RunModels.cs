using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Avalonia.Media;
using BankingThreads.Core;

namespace BankingThreads.UI.Presentation;

public static class Display
{
    public static string Money(decimal value) => value.ToString("C2", CultureInfo.GetCultureInfo("en-US"));
    public static string Difference(decimal value) => (value > 0 ? "+" : "") + Money(value);
    public static IBrush Brush(string hex) => SolidColorBrush.Parse(hex);
}

public sealed class WorkerModel : INotifyPropertyChanged
{
    public static readonly (string Id, string Title, string Rate, string Symbol, string Color)[] Definitions =
    {
        ("interest", "Interest", "+2.0%", "+", "#3478F6"),
        ("management-fee", "Management fee", "−1.5%", "−", "#9B6AE2"),
        ("compound-yield", "Compound yield", "+1.0%", "↗", "#20A38B"),
        ("tax", "Tax on balance", "−10.0%", "%", "#DB934B"),
        ("cola", "Cost of living", "+0.5%", "≈", "#DF7797")
    };

    private WorkerPhase? _phase;
    public string Id { get; }
    public string Title { get; }
    public string Rate { get; }
    public string Symbol { get; }
    public IBrush Accent { get; }
    public IBrush IconBackground { get; }
    public string Status => _phase switch
    {
        WorkerPhase.Started => "Started",
        WorkerPhase.Waiting => "Waiting for lock",
        WorkerPhase.Working => "Working",
        WorkerPhase.Completed => "Completed",
        WorkerPhase.Failed => "Stopped with error",
        _ => "Ready"
    };
    public IBrush StatusBrush => Display.Brush(_phase switch
    {
        WorkerPhase.Completed => "#208663",
        WorkerPhase.Working => "#3478F6",
        WorkerPhase.Waiting => "#A8752C",
        WorkerPhase.Failed => "#C34F50",
        _ => "#8D929F"
    });
    public IBrush CardBorder => Display.Brush(_phase == WorkerPhase.Working ? "#9EBFFB" : "#E7E9EF");
    public WorkerPhase? Phase => _phase;
    public event PropertyChangedEventHandler? PropertyChanged;

    public WorkerModel(string id, string title, string rate, string symbol, string color)
    {
        Id = id; Title = title; Rate = rate; Symbol = symbol;
        Accent = Display.Brush(color);
        IconBackground = new SolidColorBrush(Color.Parse(color), 0.1);
    }

    public void SetPhase(WorkerPhase? phase)
    {
        _phase = phase;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public static string NameFor(string id) => Definitions.FirstOrDefault(x => x.Id == id).Title ?? id;
}

public sealed class EventModel
{
    public SimulationEvent Source { get; }
    public string Time => $"{Source.Elapsed.TotalMilliseconds:0} ms";
    public string Worker => WorkerModel.NameFor(Source.Update.WorkerId);
    public string Detail => Source.Update.Phase switch
    {
        WorkerPhase.Started => "Started",
        WorkerPhase.Waiting => "Waiting for lock",
        WorkerPhase.Working => Source.Update.Detail.Contains("without a lock")
            ? "Updating without lock" : "Updating with lock",
        WorkerPhase.Completed => "Finished",
        _ => Source.Update.Detail
    };
    public string Balance => Source.Update.ObservedBalance is decimal value ? Display.Money(value) : "—";
    public IBrush Dot => Display.Brush(Source.Update.Phase switch
    {
        WorkerPhase.Completed => "#20A38B",
        WorkerPhase.Waiting => "#D6A45A",
        WorkerPhase.Working => "#3478F6",
        _ => "#B9BFCA"
    });
    public EventModel(SimulationEvent source) => Source = source;
}

public sealed record RunRecord(int Number, DateTimeOffset RecordedAt, SimulationResult Result)
{
    public string NumberLabel => $"{Number:00}";
    public string Mode => Result.Synchronized ? "Synchronized" : "Unsynchronized";
    public string Time => RecordedAt.ToLocalTime().ToString("h:mm:ss tt");
    public string Balance => Display.Money(Result.ActualBalance);
    public string Difference => Display.Difference(Result.ActualBalance - Result.ReferenceBalance);
    public string Duration => $"{Result.Duration.TotalMilliseconds:0} ms";
    public IBrush ModeBrush => Display.Brush(Result.Synchronized ? "#3478F6" : "#A77836");
    public IBrush ModeBackground => Display.Brush(Result.Synchronized ? "#ECF2FE" : "#FCF5E9");
}

public static class SessionExport
{
    // A single CSV contains both summary and event rows, plus the test environment.
    public static string CreateCsv(IEnumerable<RunRecord> records)
    {
        var output = new StringBuilder();
        output.AppendLine("row_type,run,recorded_at,mode,starting_balance,reference_balance,actual_balance,difference,duration_ms,event_sequence,event_ms,worker,phase,detail,observed_balance,os,dotnet");
        foreach (var run in records.OrderBy(x => x.Number))
        {
            var r = run.Result;
            string[] Prefix(string type) => new[]
            {
                type, run.Number.ToString(CultureInfo.InvariantCulture), run.RecordedAt.ToString("O"),
                run.Mode, Number(r.StartingBalance), Number(r.ReferenceBalance), Number(r.ActualBalance),
                Number(r.ActualBalance - r.ReferenceBalance), r.Duration.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)
            };
            void Row(IEnumerable<string> values) => output.AppendLine(string.Join(",", values.Select(Escape)));
            Row(Prefix("summary").Concat(new[] { "", "", "", "", "", "", Environment.OSVersion.ToString(), Environment.Version.ToString() }));
            foreach (var item in r.Events)
                Row(Prefix("event").Concat(new[]
                {
                    item.Sequence.ToString(CultureInfo.InvariantCulture),
                    item.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture),
                    item.Update.WorkerId, item.Update.Phase.ToString(), item.Update.Detail,
                    item.Update.ObservedBalance is decimal balance ? Number(balance) : "",
                    Environment.OSVersion.ToString(), Environment.Version.ToString()
                }));
        }
        return output.ToString();
    }

    private static string Number(decimal value) => value.ToString("F2", CultureInfo.InvariantCulture);
    private static string Escape(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}
