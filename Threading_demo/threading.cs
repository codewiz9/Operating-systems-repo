//the follwoing lines are used to import the necessary libraries for the program
using System;
using System.Threading;

//the main class for the program
//each function will have 5 threads except priority function which will have 3 threads
partial class Program
{
    private static readonly object _ledgerLock = new object(); // Shared lock object; readonly prevents reassignment.
    protected static decimal shared_balance = 2000.00m;

    //main function
    protected static void Main(string[] args)
    {
        Console.WriteLine($"starting balance: {starting_balance:C}");

        // A fixed-order reference. Rounding can produce cents of variation in other valid orders.
        decimal expected = expected_balance();
        Console.WriteLine("\n=== UNSYNCHRONIZED RUN ===");

        // Without a lock, updates may be lost. An individual run can still match the reference.
        for (int i = 1; i <= 5; i++)
        {
            decimal result = Sync_Controller(false);
            Console.WriteLine($"run {i}: expected {expected:C} | actual {result:C} | diff {result - expected:C}");
        }

        Console.WriteLine("\n=== SYNCHRONIZED RUN ===");
        
        // synced: each thread holds lock, so no updates are lost
        for (int i = 1; i <= 5; i++)
        {
            decimal result = Sync_Controller(true);
            Console.WriteLine($"run {i}: expected {expected:C} | actual {result:C} | diff {result - expected:C}");
        }
    }

    // run the 5 calculations sequentially on one thread, compare the result to multi-threaded
    protected static decimal expected_balance()
    {
        shared_balance = starting_balance;
        compute_interest();
        compute_management_fee();
        compute_compound_yield();
        compute_tax();
        compute_cost_of_living_adjustment();
        return shared_balance;
    }

    //Computes I = P * r * t and adds the earned interest
    protected static void compute_interest(){
        //loop to make sure the threads run long enough to simulate concurent activity
        for (int i = 0; i <= 3; i++){
            //calculate the interest (I = P * r for one period)
            decimal interest = shared_balance * 0.02m;
            //add the interest to the balance
            decimal new_balance = Math.Round(shared_balance + interest, 2);
            //update the balance
            shared_balance = new_balance;
            //delay simulation
            Thread.Sleep(30);
        }
    }
    //Computes a 1.5% management fee and subtracts it
    protected static void compute_management_fee(){
        //loop to make sure the threads run long enough to simulate concurent activity
        for (int i = 0; i <= 3; i++){
            //calculate the management fee
            decimal management_fee = Math.Round(shared_balance * 0.015m, 2);
            //subtract the management fee from the balance
            decimal new_balance = Math.Round(shared_balance - management_fee, 2);
            //update the balance
            shared_balance = new_balance;
            //delay simulation
            Thread.Sleep(30);
        }
    }
    //Computes one period of compound yield: P * (1 + r) - P and adds it
    protected static void compute_compound_yield(){
        //loop to make sure the threads run long enough to simulate concurent activity
        for (int i = 0; i <= 3; i++){
           decimal yield_rate = 0.01m; 
           decimal yield = Math.Round(shared_balance * yield_rate, 2);
           decimal new_balance = Math.Round(shared_balance + yield, 2);
           //update the balance
           shared_balance = new_balance;
           //delay simulation
           Thread.Sleep(30);
        }
    }
    //Calculates a 10% tax on the current balance and subtracts it
    protected static void compute_tax(){
        //loop to make sure the threads run long enough to simulate concurent activity
        for (int i = 0; i <= 3; i++){
            //calculate the tax
            decimal tax = Math.Round(shared_balance * 0.10m, 2);
            //subtract the tax from the balance
            decimal new_balance = Math.Round(shared_balance - tax, 2);
            //update the balance
            shared_balance = new_balance;
            //delay simulation
            Thread.Sleep(30);
        }
    }
    //Calculates a 0.5% cost-of-living adjustment and adds it
    protected static void compute_cost_of_living_adjustment(){
        //loop to make sure the threads run long enough to simulate concurent activity
        for (int i = 0; i <= 3; i++){
            //calculate the cost-of-living adjustment
            decimal cost_of_living_adjustment = Math.Round(shared_balance * 0.005m, 2);
            //add the cost-of-living adjustment to the balance
            decimal new_balance = Math.Round(shared_balance + cost_of_living_adjustment, 2);
            //update the balance
            shared_balance = new_balance;
            //delay simulation
            Thread.Sleep(30);
        }
 
        }

        //priorty controller
        protected static void priority_controler()
    {
        Action[] calculations =
        {
            compute_interest,
            compute_compound_yield,
            compute_management_fee,
            compute_cost_of_living_adjustment,
            compute_tax,
        };

        string[] names =
        {
            "high-priority-interest",
            "above-medium-priority-compound-yield",
            "medium-priority-management-fee",
            "below-medium-priority-cost-of-living-adjustment",
            "low-priority-tax"
        };

        ThreadPriority[] priorities =
        {
            ThreadPriority.Highest,
            ThreadPriority.AboveNormal,
            ThreadPriority.Normal,
            ThreadPriority.BelowNormal,
            ThreadPriority.Lowest
        };

        Thread[] workers = new Thread[calculations.Length];

        for(int i = 0; i < calculations.Length; i++)
        {
            int index = i; //Captures the current index for thread
            workers[i] = new Thread(() =>
            {
                Console.WriteLine(
                    $"[{Thread.CurrentThread.Name}] running at {Thread.CurrentThread.Priority}");
                calculations[index]();
            });

            workers[i].Name = names[i];
            workers[i].Priority = priorities[i];
        }
        foreach (Thread worker in workers)
            worker.Start();
        
        foreach (Thread worker in workers)
            worker.Join();
    }
}

