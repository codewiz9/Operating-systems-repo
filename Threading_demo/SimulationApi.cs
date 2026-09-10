using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BankingThreads.Core
{
    public enum WorkerPhase { Started, Waiting, Working, Completed, Failed }

    public record WorkerUpdate(string WorkerId, WorkerPhase Phase, string Detail,
        decimal? ObservedBalance = null);

    public record SimulationEvent(int Sequence, TimeSpan Elapsed, WorkerUpdate Update);

    public record SimulationResult(bool Synchronized, decimal StartingBalance,
        decimal ReferenceBalance, decimal ActualBalance, TimeSpan Duration,
        IReadOnlyList<SimulationEvent> Events);

    // The UI calls this API from a background task. It does not replace the five workers.
    public static class SimulationApi
    {
        private static readonly object runGate = new object();
        public static decimal StartingBalance => global::Program.StartingBalanceForUi;

        public static SimulationResult Run(bool synchronized,
            Action<SimulationEvent> progress = null)
        {
            // The legacy simulation has a static balance: serialize whole experiments.
            // This gate is separate from the optional lock used by the worker threads.
            lock (runGate)
            {
                decimal reference = global::Program.ReferenceForUi();
                var events = new List<SimulationEvent>();
                var eventGate = new object();
                var clock = Stopwatch.StartNew();

                void Report(WorkerUpdate update)
                {
                    // Sequence is report order, not a claim about OS scheduling order.
                    lock (eventGate)
                    {
                        var entry = new SimulationEvent(events.Count + 1, clock.Elapsed, update);
                        events.Add(entry);
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

partial class Program
{
    internal static decimal StartingBalanceForUi => starting_balance;
    internal static decimal ReferenceForUi() => expected_balance();
    internal static decimal RunWorkersForUi(bool synchronized,
        Action<BankingThreads.Core.WorkerUpdate> progress) => Sync_Controller(synchronized, progress);
}
