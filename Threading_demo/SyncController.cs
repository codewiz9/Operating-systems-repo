using System;
using System.Threading;
using System.Collections.Concurrent;
using BankingThreads.Core;

partial class Program
{
    // a constant starting balance for the shared money
    private const decimal starting_balance = 2000.00m;

    // launches the 5 calculations from threading.cs, each on its own thread
    // use_lock == false -> UNSYNCHRONIZED, threads race, updates get lost
    // use_lock == true  -> SYNCHRONIZED, each thread holds the lock, no updates lost
    // returns the final balance so the caller (Main, or a frontend) decides how to show it
    // The UI supplies a callback: a function we call when a worker's status changes.
    // Console callers can omit it and continue receiving text output.
    protected static decimal Sync_Controller(bool use_lock, Action<WorkerUpdate> progress = null)
    {
        // reset the shared money to a const starting point
        shared_balance = starting_balance;

        // the 5 calculations, and a matching name for each thread
        Action[] calculations =
        {
            compute_interest,
            compute_management_fee,
            compute_compound_yield,
            compute_tax,
            compute_cost_of_living_adjustment,
        };
        string[] names =
        {
            "interest",
            "management-fee",
            "compound-yield",
            "tax",
            "cola",
        };

        // one thread per calculation
        Thread[] workers = new Thread[calculations.Length];
        // Workers may fail at the same time, so collect errors in a thread-safe queue.
        var errors = new ConcurrentQueue<Exception>();
        for (int i = 0; i < calculations.Length; i++)
        {
            // capture what method will be ran, and its name
            Action calculation = calculations[i];
            string name = names[i];

            workers[i] = new Thread(() =>
            {
                try
                {
                    run_one(calculation, use_lock, progress);
                }
                catch (Exception error)
                {
                    // An exception on this Thread does not automatically reach the UI's
                    // try/catch. Save it so the controller can report it after joining.
                    errors.Enqueue(error);
                }
            });
            workers[i].Name = name;
        }

        // Start all five before joining. Their actual execution order is up to the OS.
        foreach (Thread worker in workers)
            worker.Start();
        foreach (Thread worker in workers)
            worker.Join();

        // All workers have stopped. Send any failures back to the caller instead of
        // returning an incomplete balance as a successful result. The UI shows the error.
        if (!errors.IsEmpty)
        {
            throw new AggregateException(
                "A banking worker could not complete its calculation.", errors);
        }

        return shared_balance;
    }

    // runs one calculation on the current thread, optionally holding the ledger lock
    private static void run_one(Action calculation, bool use_lock, Action<WorkerUpdate> progress)
    {
        string me = Thread.CurrentThread.Name;

        // These reports supply the UI's worker status, activity rows, and chart samples.
        // They run on this worker thread; the UI schedules screen updates on its own thread.
        // A missing balance means this is a status update, not a chart sample.
        void Report(WorkerPhase phase, string detail, decimal? balance = null)
        {
            if (progress != null)
            {
                progress(new WorkerUpdate(me, phase, detail, balance));
            }
            else
            {
                // Output for the original console app when no callback was supplied.
                Console.WriteLine($"  [{me}] {detail}" +
                    (balance.HasValue ? $" (balance {balance.Value:C})" : ""));
            }
        }

        Report(WorkerPhase.Started, "Thread started");

        if (use_lock)
        {
            Report(WorkerPhase.Waiting, "Requesting the account lock");
            // Mutual exclusion, not a promise of FIFO ordering.
            lock (_ledgerLock)
            {
                Report(WorkerPhase.Working, "Lock acquired · applying four updates");
                calculation();
                // Capture the balance while this worker still holds the account lock.
                Report(WorkerPhase.Completed, "Protected calculation finished", shared_balance);
            }
        }
        else
        {
            // no lock 
            Report(WorkerPhase.Working, "Applying four updates without a lock");
            calculation();
            // Another worker may change this balance; that is part of the unprotected run.
            Report(WorkerPhase.Completed, "Unprotected calculation finished", shared_balance);
        }
    }
}
