using System;
using System.Threading;

partial class Program
{
    // a constant starting balance for the shared money
    private const decimal starting_balance = 2000.00m;

    // launches the 5 calculations from threading.cs, each on its own thread
    // use_lock == false -> UNSYNCHRONIZED, threads race, updates get lost
    // use_lock == true  -> SYNCHRONIZED, each thread holds the lock, no updates lost
    // returns the final balance so the caller (Main, or a frontend) decides how to show it
    protected static decimal Sync_Controller(bool use_lock)
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
        for (int i = 0; i < calculations.Length; i++)
        {
            // capture what method will be ran, and its name
            Action calculation = calculations[i];
            string name = names[i];

            workers[i] = new Thread(() => run_one(calculation, use_lock));
            workers[i].Name = name;
        }

        // start all 5 at once, then wait for all 5
        foreach (Thread worker in workers)
            worker.Start();
        foreach (Thread worker in workers)
            worker.Join();

        return shared_balance;
    }

    // runs one calculation on the current thread, optionally holding the ledger lock
    private static void run_one(Action calculation, bool use_lock)
    {
        string me = Thread.CurrentThread.Name;

        if (use_lock)
        {
            // threads queue here; only one is inside the lock at a time
            lock (_ledgerLock)
            {
                Console.WriteLine($"  [{me}] has lock, started");
                calculation();
                Console.WriteLine($"  [{me}] finished, lock released (balance {shared_balance:C})");
            }
        }
        else
        {
            // no lock 
            Console.WriteLine($"  [{me}] started (no lock)");
            calculation();
            Console.WriteLine($"  [{me}] finished (balance {shared_balance:C})");
        }
    }
}
