//the follwoing lines are used to import the necessary libraries for the program
using System;
using System.Threading;
using System.Threading.Tasks;

//the main class for the program
//each function will have 5 threads except priority function which will have 3 threads
partial class Program
{
    private static readonly object _ledgerLock = new object(); // read only lock for the ledger aka this cotnrols sync
    protected static decimal shared_balance = 2000.00m;

    //main function
    protected static void Main(string[] args){

        
        Console.WriteLine($"starting balance: {shared_balance:C}");

        // unsynced: 5 threads race on shared_balance with no lock, so the final total differs each run
        for (int i = 1; i <= 5; i++)
        {
            Console.Write($"run {i}: ");
            Sync_Controller(false);
        }

        // synced: each thread holds lock, so no updates are lost
        for (int i = 1; i <= 5; i++)
        {
            Console.Write($"run {i}: ");
            Sync_Controller(true);
        }
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
    //Calculates a 10% tax on accrued profits and subtracts it
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

        //priorty controler
        protected static void priority_controler(){
        }
    }


