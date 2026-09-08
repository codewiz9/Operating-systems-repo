//the follwoing lines are used to import the necessary libraries for the program
using System;
using System.Threading;
using System.Threading.Tasks;

//the main class for the program
//each function will have 5 threads except priority function which will have 3 threads
class Program
{
    private static readonly object _ledgerLock = new object(); // read only lock for the ledger aka this cotnrols sync
    protected static decimal shared_balance = 2000.00m;
    //main function
    protected static void Main(string[] args){}

    //Computes I = P * r * t and adds the earned interest
    //Computes a 1.5% management fee and subtracts it
    //Computes one period of compound yield: P * (1 + r) - P and adds it
    //Calculates a 10% tax on accrued profits and subtracts it
    //Calculates a 0.5% cost-of-living adjustment and adds it

    //

    
}