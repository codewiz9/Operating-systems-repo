using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace BankingThreads.Core;

public static class PriorityController
{
    public static (decimal RemainingBalance, string TransactionHash) VerifySolvencyAndNonce()
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

    public static int CalculateLoanAmortization()
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

    public static int LowPriorityTransactions()
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
}
