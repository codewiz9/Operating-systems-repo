# Ledger · Banking concurrency lab

A C# operating-systems project with an Avalonia desktop interface. Five banking
workers update one shared account, with and without mutual exclusion. The original
console demonstration is still available.

## Run the graphical app

Install the **.NET 10 SDK**, then run from the repository root:

```sh
dotnet run --project BankingThreads.UI/BankingThreads.UI.csproj --configuration Release
```

The same command works on Windows and macOS. The assignment's demonstration and
scheduling observations must be tested on **Windows**. Avalonia packages restore
automatically; no additional UI template installation is needed to run this repo.

On the current Mac, the installed x64 SDK is in `/usr/local/share/dotnet/x64`. If
the shell cannot find it, use `export PATH="/usr/local/share/dotnet/x64:$PATH"` or
run that folder's `dotnet` executable directly. A new installation on Apple Silicon
should use Microsoft's Arm64 SDK.

## Use the app

- **Overview:** choose a synchronization mode and run one experiment or five trials.
- **Workers:** real started, waiting, working, and completed states from the simulation.
- **Chart:** observed balances at worker completion, plus starting and final values.
- **Run history:** inspect earlier runs from this window's session.
- **Export session:** save CSV summary and event rows, including OS and .NET information.
- **How it works:** definitions and limitations for the team demonstration.
- **Scheduling:** clearly marked pending; the team's `priority_controler` is still empty.

Runs are sequential at the experiment level because the existing account is static.
The UI remains responsive while the workers run. Closing during a batch finishes
the current experiment, skips the rest, and then closes. History is in memory;
export before closing if you want to preserve it.

## Run the console demonstration

```sh
dotnet run --project Threading_demo/Threading_demo.csproj --configuration Release
```

## Verify the integration

```sh
dotnet run --project BankingThreads.Checks/BankingThreads.Checks.csproj --configuration Release
```

This executable uses Avalonia's headless renderer. It checks both modes, worker
events, duplicate-click prevention, five-trial batches, history replay, CSV content,
navigation, minimum-size layout, and closing during a run. It saves screenshots and
a real sample CSV under `artifacts/ui-checks/` (ignored by Git). It does not exercise
the native OS save dialog or replace final testing on Windows.

## Project map

| Location | Responsibility |
| --- | --- |
| `Threading_demo/threading.cs` | Existing calculations, reference calculation, console entry point. |
| `Threading_demo/SyncController.cs` | Creates and joins five workers; optional account lock; worker reports. |
| `Threading_demo/SimulationApi.cs` | Public result/event types; safe entry point for whole UI experiments. |
| `BankingThreads.UI/MainWindow.axaml` | Window layout and views. |
| `BankingThreads.UI/MainWindow.axaml.cs` | Interaction, background runs, UI updates, and export. |
| `BankingThreads.UI/Styles/Ledger.axaml` | Shared colors, buttons, cards, and typography. |
| `BankingThreads.UI/Controls/BalanceChart.cs` | Draws actual reported balance samples. |
| `BankingThreads.UI/Presentation/RunModels.cs` | Worker display state, history records, and CSV formatting. |
| `BankingThreads.Checks` | Automated integration and rendering checks. |

See [the UI walkthrough](docs/UI-WALKTHROUGH.md) for a plain-language explanation
of the C# and how the projects communicate.

## Explain the evidence accurately

- Each job applies its calculation **four** times (`i = 0` through `i = 3`).
- The current tax calculation subtracts 10% of the **whole balance** each iteration.
- The synchronized lock protects an entire worker method, including the pauses.
  It provides mutual exclusion, not FIFO order or parallel calculation throughput.
- The fixed-order reference is $1,419.38. Different protected worker orders can
  produce $1,419.36–$1,419.39 because of rounding. The UI reports differences
  neutrally; it does not label every difference a race-condition failure.
- An unsynchronized run can match the reference by chance. Repeat observations.
- Events are ordered by receipt at the reporting callback. Reporting can affect
  scheduling, and chart points are completion snapshots, not an atomic transaction ledger.
- Reported duration measures the workers; it excludes computing the reference.
- Thread-priority investigation and the team's contribution statement are still
  required before submission. Do not present the scheduling placeholder as a finished experiment.

The interface, adapter, and integration checks were developed with AI assistance.
Each team member should review and be able to explain submitted code, and the team
should document its own contributions as required by the assignment.
