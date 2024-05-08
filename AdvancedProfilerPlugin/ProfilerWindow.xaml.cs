using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace AdvancedProfiler;

/// <summary>
/// Interaction logic for ProfilerWindow.xaml
/// </summary>
public partial class ProfilerWindow : Window, INotifyPropertyChanged
{
    static Thread? windowThread;
    static ProfilerWindow? window;

    const int maxRecordingSeconds = 60;
    const int maxRecordingFrames = 3600;

    Timer? recordingTimer;

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    bool IsRecordTimeValid()
    {
        if (recordTimeTypeBox.SelectedIndex == 0) // Seconds
        {
            if (float.TryParse(recordTimeBox.Text, out float seconds))
            {
                if (seconds > 0 && seconds <= maxRecordingSeconds)
                    return true;
            }
        }
        else if (recordTimeTypeBox.SelectedIndex == 1) // Frames
        {
            if (int.TryParse(recordTimeBox.Text, out int frames))
            {
                if (frames > 0 && frames <= maxRecordingFrames)
                    return true;
            }
        }

        return false;
    }

    public bool RecordTimeInvalid => !IsRecordTimeValid();

    public bool CanStartStopRecording
    {
        get
        {
            if (Profiler.IsRecordingEvents)
                return true; // Allow stop button

            return IsRecordTimeValid();
        }
    }

    internal static void Open(Window mainWindow)
    {
        var pos = new Point(mainWindow.Left, mainWindow.Top);
        var centerParent = new Point(pos.X + mainWindow.Width / 2, pos.Y + mainWindow.Height / 2);

        if (windowThread == null)
        {
            windowThread = new Thread(ThreadStart);
            windowThread.Name = "Profiler UI Thread";
            windowThread.SetApartmentState(ApartmentState.STA);
            windowThread.Start(centerParent);
        }
        else if (window != null)
        {
            window.Dispatcher.BeginInvoke(() =>
            {
                window.Activate();

                if (window.WindowState == WindowState.Minimized)
                    window.WindowState = WindowState.Normal;
            });
        }
        else
        {
            Dispatcher.CurrentDispatcher.BeginInvoke(() =>
            {
                window = new ProfilerWindow();
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = centerParent.X - window.Width / 2;
                window.Top = centerParent.Y - window.Height / 2;
                window.Show();
            });
        }
    }

    static void ThreadStart(object? startObj)
    {
        var centerParent = (Point)startObj!;

        window = new ProfilerWindow();
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = centerParent.X - window.Width / 2;
        window.Top = centerParent.Y - window.Height / 2;
        window.Show();

        Dispatcher.Run();
    }

    public ProfilerWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(CancelEventArgs args)
    {
        base.OnClosing(args);

        window = null;
    }

    void RecordTimeTypeBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs args)
    {
        if (recordTimeTypeBox.SelectedIndex == 0) // Seconds
        {
            if (int.TryParse(recordTimeBox.Text, out int frames))
                recordTimeBox.Text = (frames * (1f / 60)).ToString();
            else
                recordTimeBox.Text = "0";
        }
        else if (recordTimeTypeBox.SelectedIndex == 1) // Frames
        {
            if (float.TryParse(recordTimeBox.Text, out float seconds))
                recordTimeBox.Text = ((int)(seconds * 60)).ToString();
            else
                recordTimeBox.Text = "0";
        }
    }

    bool TimeTextAllowed(string text)
    {
        bool hasDigitSep = recordTimeBox.Text.Contains(".", StringComparison.Ordinal);
        bool valid = true;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (c == '.')
            {
                if (hasDigitSep)
                {
                    valid = false;
                    break;
                }
            }
            else if (!char.IsNumber(c))
            {
                valid = false;
                break;
            }
        }

        return valid;
    }

    void RecordTimeBox_PreviewTextInput(object sender, TextCompositionEventArgs args)
    {
        args.Handled = !TimeTextAllowed(args.Text);
    }

    void RecordTimeBox_Pasting(object sender, DataObjectPastingEventArgs args)
    {
        if (args.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string)args.DataObject.GetData(typeof(string));

            if (TimeTextAllowed(text))
                return;
        }

        args.CancelCommand();
    }

    void RecordTimeBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanStartStopRecording));
    }

    void StartStopButton_Click(object sender, RoutedEventArgs args)
    {
        if (Profiler.IsRecordingEvents)
        {
            ClearTimer();
            Profiler.StopEventRecording();
            OnRecordingStopped();
        }
        else
        {
            if (recordTimeTypeBox.SelectedIndex == 0) // Seconds
            {
                if (float.TryParse(recordTimeBox.Text, out float seconds)
                    && seconds > 0 && seconds <= maxRecordingSeconds)
                {
                    Profiler.StartEventRecording();
                    ClearTimer();

                    recordingTimer = new Timer(TimerCompleted, null, TimeSpan.FromSeconds(seconds), Timeout.InfiniteTimeSpan);
                }
                else
                {
                    return;
                }
            }
            else if (recordTimeTypeBox.SelectedIndex == 1) // Frames
            {
                if (int.TryParse(recordTimeBox.Text, out int frames)
                    && frames > 0 && frames <= maxRecordingFrames)
                {
                    Profiler.StartEventRecording(frames, RecordingFramesCompleted);
                    recordingTimer = new Timer(TimerCompleted, null, TimeSpan.FromSeconds(maxRecordingSeconds), Timeout.InfiniteTimeSpan);
                }
                else
                {
                    return;
                }
            }

            frameCountLabel.Content = "";
            startStopButton.Content = "Stop Recording";

            eventsGraph.InvalidateVisual();
        }

        OnPropertyChanged(nameof(CanStartStopRecording));
    }

    void OnRecordingStopped()
    {
        startStopButton.Content = "Start Recording";
        eventsGraph.ResetZoom();

        int numRecordedFrames = 0;
        var groups = Profiler.GetProfilerGroups();

        foreach (var item in groups)
            numRecordedFrames = Math.Max(numRecordedFrames, item.NumRecordedFrames);

        frameCountLabel.Content = $"Recorded {numRecordedFrames} frames";
    }

    void ResetViewButton_Click(object sender, RoutedEventArgs args)
    {
        eventsGraph.ResetZoom();
    }

    void TimerCompleted(object? state)
    {
        if (!Profiler.IsRecordingEvents)
            return;

        Profiler.StopEventRecording();
        ClearTimer();
        Dispatcher.BeginInvoke(OnRecordingStopped);
    }

    void RecordingFramesCompleted()
    {
        ClearTimer();
        Dispatcher.BeginInvoke(OnRecordingStopped);
    }

    void ClearTimer()
    {
        if (recordingTimer == null)
            return;

        recordingTimer.Change(Timeout.Infinite, Timeout.Infinite); // Cancel timer
        recordingTimer.Dispose();
        recordingTimer = null;
    }
}
