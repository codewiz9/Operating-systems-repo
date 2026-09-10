using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BankingThreads.Core
{
    public enum WorkerPhase { Started, Waiting, Working, Completed, Failed }

    // A small data message from one worker. The controller does not know about UI controls.
    public record WorkerUpdate(string WorkerId, WorkerPhase Phase, string Detail,
        decimal? ObservedBalance = null);

    // The API adds a report number and elapsed time for the activity table and chart.
    public record SimulationEvent(int Sequence, TimeSpan Elapsed, WorkerUpdate Update);

    // The completed experiment's data, used by the result display, history, and CSV export.
    public record SimulationResult(bool Synchronized, decimal StartingBalance,
        decimal ReferenceBalance, decimal ActualBalance, TimeSpan Duration,
        IReadOnlyList<SimulationEvent> Events);

    // The UI calls this method from Task.Run so waiting for workers does not freeze the window.
    // This class connects the existing controller to the UI using plain C# data and callbacks.
    public static class SimulationApi
    {
        private static readonly object runGate = new object();
        public static decimal StartingBalance => global::Program.StartingBalanceForUi;

        public static SimulationResult Run(bool synchronized,
            Action<SimulationEvent> progress = null)
        {
            // The account balance is shared. Allow one experiment through this API at a time
            // so a second experiment cannot reset the balance while the first is running.
            // This does not lock individual workers in an unsynchronized experiment.
            lock (runGate)
            {
                decimal reference = global::Program.ReferenceForUi();
                var events = new List<SimulationEvent>();
                var eventGate = new object();
                // Measure the worker run separately from the reference calculation above.
                var clock = Stopwatch.StartNew();

                void Report(WorkerUpdate update)
                {
                    // Multiple workers can report together. Protect the event list and its
                    // numbering. These numbers describe report order, not OS scheduling order.
                    lock (eventGate)
                    {
                        var entry = new SimulationEvent(events.Count + 1, clock.Elapsed, update);
                        events.Add(entry);
                        // ?.Invoke calls the UI's callback if one was supplied. It still runs
                        // on the reporting thread; the UI uses Dispatcher.UIThread.Post to draw.
                        progress?.Invoke(entry);
                    }
                }

                decimal actual = global::Program.RunWorkersForUi(synchronized, Report);
                clock.Stop();
                return new SimulationResult(synchronized, StartingBalance, reference,
                    actual, clock.Elapsed, events.AsReadOnly());
            }
        }
    }
}

// These small methods expose the existing calculations to SimulationApi.
// "partial" makes this part of the same Program class as threading.cs and SyncController.cs.
partial class Program
{
    internal static decimal StartingBalanceForUi => starting_balance;
    internal static decimal ReferenceForUi() => expected_balance();
    internal static decimal RunWorkersForUi(bool synchronized,
        Action<BankingThreads.Core.WorkerUpdate> progress) => Sync_Controller(synchronized, progress);
}
