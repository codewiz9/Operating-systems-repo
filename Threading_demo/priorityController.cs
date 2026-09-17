using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

partial class Program
{
    protected static (decimal RemainingBalance, string TransactionHash) VerifySolvencyAndNonce()
    {
        decimal current_balance = 5000.00m;
        decimal withdrawal_amount = 1000.00m;

        bool isSolvent = (current_balance - withdrawal_amount) > 0;
        decimal new_balance = isSolvent ? Math.Round(current_balance - withdrawal_amount, 2) : current_balance;

        string rawData = $"tx_{new_balance}_{DateTime.UtcNow.Ticks}";
        string hashHex;

        using (var sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            hashHex = Convert.ToHexString(bytes);
        }

        return (new_balance, hashHex);
    }

    protected static int CalculateLoanAmortization()
    {
        double principal = 250000.0;
        double annualRate = 0.065;
        double monthlyRate = annualRate / 12.0;
        int cpu_laod = 0;
        int months = 360;

        for (int i = 0; i < 10000; i++)
        {
            double numerator = monthlyRate * Math.Pow(1 + monthlyRate, months);
            double denominator = Math.Pow(1 + monthlyRate, months) - 1;
            double payment = principal * (numerator / denominator);
            cpu_laod += 1;
        }

        return cpu_laod;
    }

    protected static int LowPriorityTransactions()
    {
        Random random = new Random();
        decimal account_balance = 10000.00m;
        int cpu_laod = 0;

        for (int i = 0; i < 100000; i++)
        {
            decimal transaction_amount = random.Next(-5000, 5000);
            account_balance += transaction_amount;
            cpu_laod += 1;
            Thread.Sleep(10);
        }

        return cpu_laod;
    }

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

        for (int i = 0; i < calculations.Length; i++)
        {
            int index = i;
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
