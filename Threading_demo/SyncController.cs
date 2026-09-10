using System;
using System.Threading;

partial class Program
{
    // a constant starting balance for the shared money
    private const decimal starting_balance = 2000.00m;

    // launches the 5 calculations from threading.cs, each on its own thread
    // use_lock == false -> UNSYNCHRONIZED, threads race, updates get lost
    // use_lock == true  -> SYNCHRONIZED, each thread holds the lock, no updates lost
    protected static void Sync_Controller(bool use_lock)
    {
        // reset the shared money to a const starting point
        shared_balance = starting_balance;

        // the 5 calculations as a list so one loop covers them all
        Action[] calculations =
        {
            compute_interest,
            compute_management_fee,
            compute_compound_yield,
            compute_tax,
            compute_cost_of_living_adjustment,
        };

        // one thread per calculation
        Thread[] workers = new Thread[calculations.Length];
        for (int i = 0; i < calculations.Length; i++)
        {
            // each thread should captures its own calculation
            Action calculation = calculations[i];

            workers[i] = new Thread(() =>
            {
                if (use_lock)
                {
                    // only one thread inside here at a time, the other 4 wait
                    lock (_ledgerLock)
                    {
                        calculation();
                    }
                }
                else
                {
                    // no lock, runs alongside the others
                    calculation();
                }
            });
        }

        // start them all running at once
        foreach (Thread worker in workers)
            worker.Start();

        // wait for all 5 to finish before reporting the final balance
        foreach (Thread worker in workers)
            worker.Join();

        // report the balance, ternary operator used
        string mode = use_lock ? "SYNCHRONIZED" : "UNSYNCHRONIZED";
        Console.WriteLine($"[{mode}] final balance: {shared_balance:C}");
    }
}
