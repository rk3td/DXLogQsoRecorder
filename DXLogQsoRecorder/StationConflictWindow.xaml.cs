using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace DXLogQsoRecorder;

public partial class StationConflictWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _secondsRemaining = 20;
    private bool _completed;

    public event Action<StationConflictChoice>? SelectionCompleted;

    public StationConflictWindow(string currentStation, string newStation)
    {
        InitializeComponent();
        StationInfoText.Text = $"Current station: {currentStation}\nNew station: {newStation}";
        SwitchButtonText.Text = $"Switch to {newStation}";
        UpdateCountdown();
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _secondsRemaining--;
        if (_secondsRemaining <= 0)
        {
            Complete(StationConflictChoice.KeepCurrent);
            return;
        }
        UpdateCountdown();
    }

    private void UpdateCountdown() =>
        CountdownText.Text = $"Keeping the current station in {_secondsRemaining} seconds...";

    private void KeepCurrent_Click(object sender, RoutedEventArgs e) => Complete(StationConflictChoice.KeepCurrent);
    private void Switch_Click(object sender, RoutedEventArgs e) => Complete(StationConflictChoice.SwitchToNew);
    private void RecordAll_Click(object sender, RoutedEventArgs e) => Complete(StationConflictChoice.RecordAll);

    private void Complete(StationConflictChoice choice)
    {
        if (_completed) return;
        _completed = true;
        _timer.Stop();
        SelectionCompleted?.Invoke(choice);
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_completed)
        {
            _completed = true;
            _timer.Stop();
            SelectionCompleted?.Invoke(StationConflictChoice.KeepCurrent);
        }
        base.OnClosing(e);
    }
}
