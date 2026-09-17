using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using BankingThreads.Core;
using BankingThreads.UI.Presentation;

namespace BankingThreads.UI.Services;

public sealed record PriorityWork(PriorityWorkerDefinition Definition, Func<PriorityWorkResult> Execute,
    bool InterruptibleSleep = false);

// The UI owns these three threads and calls the original workload methods through the API.
// The older five-method priority_controler launcher is intentionally not used.
public static class PriorityRunner
{
    public static IReadOnlyList<PriorityWork> Workloads { get; } = Array.AsReadOnly(new[]
    {
        new PriorityWork(PriorityWorkerModel.Definitions[0], () =>
        {
            var result = SimulationApi.VerifySolvencyAndNonce();
            return new PriorityWorkResult(result.RemainingBalance, result.TransactionHash);
        }),
        new PriorityWork(PriorityWorkerModel.Definitions[1], () =>
            new PriorityWorkResult(Iterations: SimulationApi.CalculateLoanAmortization())),
        new PriorityWork(PriorityWorkerModel.Definitions[2], () =>
            new PriorityWorkResult(Iterations: SimulationApi.LowPriorityTransactions()), InterruptibleSleep: true)
    });

    public static PriorityRunResult Run(Action<PriorityUpdate>? progress, CancellationToken cancellationToken,
        bool useAssignedPriorities = true) => Run(Workloads, progress, cancellationToken, useAssignedPriorities);

    // Accepting the workload list also lets checks exercise failure/completion paths with
    // short fixtures; the application always uses the three original methods above.
    public static PriorityRunResult Run(IReadOnlyList<PriorityWork> workloads,
        Action<PriorityUpdate>? progress, CancellationToken cancellationToken, bool useAssignedPriorities = true)
    {
        var clock = Stopwatch.StartNew();
        var events = new List<PriorityUpdate>();
        var eventGate = new object();
        Exception? reportingError = null;
        var started = new List<Thread>();
        var registrations = new List<CancellationTokenRegistration>();

        void Report(PriorityWork work, PriorityPhase phase, TimeSpan duration,
            PriorityWorkResult? result = null, string? error = null)
        {
            lock (eventGate)
            {
                var update = new PriorityUpdate(events.Count + 1, work.Definition.Method,
                    Thread.CurrentThread.Priority, Environment.CurrentManagedThreadId,
                    phase, clock.Elapsed, duration, result, error);
                events.Add(update);
                // A display error must not escape a worker and terminate the application.
                try { progress?.Invoke(update); }
                catch (ThreadInterruptedException) when (cancellationToken.IsCancellationRequested)
                {
                    // Cancellation can race the screen callback. The worker checks the
                    // token again before executing; the final event list restores the UI.
                }
                catch (Exception failure) { reportingError ??= failure; }
            }
        }

        try
        {
            foreach (var work in workloads)
            {
                var worker = new Thread(() =>
                {
                    var elapsed = Stopwatch.StartNew();
                    PriorityPhase phase;
                    PriorityWorkResult? result = null;
                    string? error = null;
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Report(work, PriorityPhase.Running, TimeSpan.Zero);
                        cancellationToken.ThrowIfCancellationRequested();
                        result = work.Execute();
                        phase = PriorityPhase.Completed;
                    }
                    catch (ThreadInterruptedException)
                    {
                        phase = PriorityPhase.Stopped;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        phase = PriorityPhase.Stopped;
                    }
                    catch (Exception failure)
                    {
                        phase = PriorityPhase.Failed;
                        error = failure.Message;
                    }
                    // A Stop request can arrive just as the workload returns. If its one
                    // interrupt reaches the reporting lock instead of Sleep, consume it and
                    // retry the terminal report so it cannot escape this owned worker.
                    while (true)
                    {
                        try { Report(work, phase, elapsed.Elapsed, result, error); break; }
                        catch (ThreadInterruptedException) { }
                    }
                })
                {
                    Name = work.Definition.Method,
                    IsBackground = true,
                    // Off keeps all three methods concurrent, with equal thread priority.
                    Priority = useAssignedPriorities ? work.Definition.Priority : ThreadPriority.Normal
                };

                worker.Start();
                started.Add(worker);
                if (work.InterruptibleSleep)
                {
                    // Only the local-data transaction workload sleeps. Interrupt wakes that
                    // existing Sleep and is caught in its wrapper above. It does not return a
                    // partial iteration count or alter the controller's loop/parameters.
                    registrations.Add(cancellationToken.Register(() =>
                    {
                        try { worker.Interrupt(); }
                        catch (ThreadStateException) { /* Already finished. */ }
                    }));
                }
            }
            foreach (var worker in started) worker.Join();
        }
        finally
        {
            // Also clean up if assigning a priority or starting a thread fails midway.
            foreach (var worker in started.Where(x => x.IsAlive && x.Name == "LowPriorityTransactions"))
            {
                try { worker.Interrupt(); }
                catch (ThreadStateException) { }
            }
            foreach (var worker in started) worker.Join();
            foreach (var registration in registrations) registration.Dispose();
        }
        clock.Stop();
        if (reportingError != null) throw new InvalidOperationException("Could not display priority progress.", reportingError);
        return new PriorityRunResult(clock.Elapsed, events.AsReadOnly(), useAssignedPriorities);
    }
}
