# How Ledger's UI fits together

The interface runs the team's existing banking calculations. It does not invent
balances, copy the calculations into the window, or replace the five workers with
animations.

## Follow one click

1. `MainWindow.axaml` declares the Run button with `Click="OnRunClick"`.
2. Avalonia calls `OnRunClick` in `MainWindow.axaml.cs`.
3. `RunExperimentsAsync` disables run/mode controls and resets the visual state.
4. `Task.Run` calls `SimulationApi.Run` away from the UI thread. Otherwise the
   controller's `Join()` calls would freeze drawing and input.
5. `SimulationApi` first computes the fixed-order reference, then calls the original
   controller. The controller creates the five actual `Thread` workers.
6. Workers report immutable `WorkerUpdate` values. The API assigns sequence numbers
   and elapsed times and stores them in an event list.
7. `Dispatcher.UIThread.Post` schedules each visible update back on Avalonia's UI
   thread, which is the thread allowed to touch controls.
8. After every worker has been joined, the API returns `SimulationResult`.
9. The UI uses that result to finish the chart, show totals, and add a history row.

## C# syntax used in the UI

| Syntax | Meaning |
| --- | --- |
| `namespace BankingThreads.UI;` | Groups UI types under a name, avoiding clashes with the console `Program`. |
| `MainWindow : Window` | Our class inherits Avalonia's existing window behavior. |
| `partial` | Another part of the same class comes from compiled XAML. |
| `InitializeComponent()` | Loads the XAML and connects named controls and events. |
| `x:Name="BalanceText"` | Lets C# access that control as `BalanceText`. |
| `BalanceText.Text = ...` | Changes a property of an existing control. |
| `async` / `await` | Allows an operation to finish later without blocking the UI thread. |
| `() => ...` | An unnamed function passed as work to execute later. |
| `Action<SimulationEvent>` | A callback that receives one event and returns nothing. |
| `record` | A concise data container with named properties. |
| `ObservableCollection<T>` | A collection that tells the UI when rows are added or removed. |
| `INotifyPropertyChanged` | Lets a worker card tell its bindings when its status changes. |
| `try` / `catch` / `finally` | Handle reported failures and always restore the controls. |
| `object?` | A reference that is allowed to be null. |

`async void` is used only for UI event handlers. The actual asynchronous run method
returns a `Task`, so failures can be observed and handled.

## The two locks have different jobs

`SimulationApi.runGate` permits one complete experiment at a time through the UI
API. The existing account balance is static, so overlapping experiments would reset
one another's money. The reference calculation also modifies that balance.

`Program._ledgerLock` is the assignment's synchronization mechanism. Only the
synchronized workers acquire it. The experiment-level gate does not protect the
individual updates in an unsynchronized run.

The event reporter also briefly locks its list to preserve a consistent reported
sequence. It does not hold that lock across the banking calculations. Like any
logging, it can influence which schedules you observe.

## Why the UI tracks a display generation

A worker can finish before all its queued screen updates have been drawn. When a
run completes, the UI rebuilds the display from the finished result and advances a
generation number. Queued callbacks check that number before updating anything.
This prevents duplicate log rows and prevents messages from a previous trial from
appearing in the next trial.

## Where to change the appearance

- `Styles/Ledger.axaml`: shared styles and the blue accent.
- `MainWindow.axaml`: spacing, columns, labels, navigation, and cards.
- `Controls/BalanceChart.cs`: chart grid, labels, fills, and real sample rendering.
- `Presentation/RunModels.cs`: worker labels, percentages, colors, and formatted text.

XAML is the description of the screen; C# is what happens when the user interacts
with it. Reuse a style when changing something common to many controls.

The layout follows Apple's guidance for [sidebars](https://developer.apple.com/design/human-interface-guidelines/sidebars),
[toolbars](https://developer.apple.com/design/human-interface-guidelines/toolbars),
and [typography](https://developer.apple.com/design/human-interface-guidelines/typography):
compact navigation with a hide/show control, a quiet toolbar, and a clear text hierarchy.
The balance sits directly on the content surface; related workers share one group.
Neutral backgrounds and darker secondary text keep the chart and primary action easy
to find. The bundled Inter font keeps rendering consistent on Mac and Windows.
These are Avalonia controls, not native macOS materials or Apple icon assets.

## What remains for the team

The priority controller is still a placeholder. Its eventual API should return
actual per-thread settings and measurements. Connect that data to the Scheduling
view only once implemented, and investigate it on Windows. Priority is not a
guarantee of execution order.

The existing financial formulas and rounding policy were preserved. A protected run
does not have to match the fixed-order reference to the cent. Keep this limitation
in the presentation, alongside the fact that the chart shows observations at worker
completion rather than every individual update.
