using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VisualProfiler.Patches;

namespace VisualProfiler;

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

    public bool ProfilePhysicsClusters
    {
        get => MyPhysics_Patches.ProfileEachCluster;
        set => MyPhysics_Patches.ProfileEachCluster = value;
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

    void RecordTimeTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs args)
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

    void RecordTimeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanStartStopRecording));
    }

    void StartStopButton_Click(object sender, RoutedEventArgs args)
    {
        if (Profiler.IsRecordingEvents)
        {
            ClearTimer();

            var recording = Profiler.StopEventRecording();

            OnRecordingStopped(recording);
        }
        else
        {
            startStopButton.Content = "Stop Recording";
            statisticsLabel.Content = "";
            outliersList.Items.Clear();
            physicsClustersList.Items.Clear();
            gridsList.Items.Clear();
            programmableBlocksList.Items.Clear();

            eventsGraph.SetRecordedEvents(null);

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
        }

        OnPropertyChanged(nameof(CanStartStopRecording));
    }

    void OnRecordingStopped(Profiler.EventsRecording recording)
    {
        startStopButton.Content = "Start Recording";

        eventsGraph.SetRecordedEvents(recording);

        outliersList.Items.Clear();
        physicsClustersList.Items.Clear();
        gridsList.Items.Clear();
        programmableBlocksList.Items.Clear();

        var analysis = ProfilerHelper.AnalyzeRecording(recording);

        statisticsLabel.Content =
            $"""
            Recording Start Time: {recording.StartTime.ToLocalTime():hh:mm:ss tt}
            Recorded {recording.NumFrames} frames
            Frame Times:
                Min: {Math.Round(analysis.FrameTimes.Min, 2)}ms
                Max: {Math.Round(analysis.FrameTimes.Max, 2)}ms
                Mean: {Math.Round(analysis.FrameTimes.Mean, 2)}ms
                StdDev: {Math.Round(analysis.FrameTimes.StdDev, 2)}ms
            """;

        var outliers = recording.GetOutlierFrames();

        foreach (int frameIndex in outliers)
        {
            var timeBounds = recording.GetTimeBoundsForFrame(frameIndex);
            long time = timeBounds.EndTime - timeBounds.StartTime;

            var item = new ListViewItem {
                Content = $"{frameIndex}: {Math.Round(ProfilerTimer.MillisecondsFromTicks(time), 2)}ms",
                Tag = frameIndex
            };

            item.MouseDoubleClick += OutlierItem_MouseDoubleClick;
            item.KeyDown += OutlierItem_KeyDown;

            outliersList.Items.Add(item);
        }

        var borderBrush = new SolidColorBrush { Color = Colors.Black };

        var copyLastPosAsGPS = new MenuItem { Header = "Copy Last Position as GPS" };
        copyLastPosAsGPS.Click += CopyLastPosAsGPS_Click;

        var clustersContextMenu = new ContextMenu();
        clustersContextMenu.Items.Add(copyLastPosAsGPS);

        foreach (var clusterInfo in analysis.PhysicsClusters.OrderByDescending(g => g.TotalTime))
        {
            var item = new ListViewItem {
                Content = clusterInfo,
                ContextMenu = clustersContextMenu,
                Margin = new Thickness(0, 0, 0, 10),
                BorderThickness = new Thickness(0, 1, 0, 0),
                BorderBrush = borderBrush
            };

            physicsClustersList.Items.Add(item);
        }

        copyLastPosAsGPS = new MenuItem { Header = "Copy Last Position as GPS" };
        copyLastPosAsGPS.Click += CopyLastPosAsGPS_Click;

        var gridsContextMenu = new ContextMenu();
        gridsContextMenu.Items.Add(copyLastPosAsGPS);

        foreach (var gridInfo in analysis.Grids.OrderByDescending(g => g.TotalTime))
        {
            var item = new ListViewItem {
                Content = gridInfo,
                ContextMenu = gridsContextMenu,
                Margin = new Thickness(0, 0, 0, 10),
                BorderThickness = new Thickness(0, 1, 0, 0),
                BorderBrush = borderBrush
            };

            gridsList.Items.Add(item);
        }

        copyLastPosAsGPS = new MenuItem { Header = "Copy Last Position as GPS" };
        copyLastPosAsGPS.Click += CopyLastPosAsGPS_Click;

        var blocksContextMenu = new ContextMenu();
        blocksContextMenu.Items.Add(copyLastPosAsGPS);

        foreach (var blockInfo in analysis.ProgrammableBlocks.OrderByDescending(g => g.TotalTime))
        {
            var item = new ListViewItem {
                Content = blockInfo,
                ContextMenu = blocksContextMenu,
                Margin = new Thickness(0, 0, 0, 10),
                BorderThickness = new Thickness(0, 1, 0, 0),
                BorderBrush = borderBrush
            };

            programmableBlocksList.Items.Add(item);
        }
    }

    void OutlierItem_MouseDoubleClick(object sender, MouseButtonEventArgs args)
    {
        var item = (ListViewItem)sender;
        int index = (int)item.Tag;

        eventsGraph.ZoomToFrame(index);
    }

    void OutlierItem_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var item = (ListViewItem)sender;
            int index = (int)item.Tag;

            eventsGraph.ZoomToFrame(index);
        }
    }

    void ResetViewButton_Click(object sender, RoutedEventArgs args)
    {
        eventsGraph.ResetZoom();
    }

    void TimerCompleted(object? state)
    {
        if (!Profiler.IsRecordingEvents)
            return;

        var recording = Profiler.StopEventRecording(ProfilerHelper.ProfilerEventObjectResolver);

        ClearTimer();
        Dispatcher.BeginInvoke(OnRecordingStopped, recording);
    }

    void RecordingFramesCompleted(Profiler.EventsRecording recording)
    {
        ClearTimer();
        Dispatcher.BeginInvoke(OnRecordingStopped, recording);
    }

    void ClearTimer()
    {
        if (recordingTimer == null)
            return;

        recordingTimer.Change(Timeout.Infinite, Timeout.Infinite); // Cancel timer
        recordingTimer.Dispose();
        recordingTimer = null;
    }

    void CopyLastPosAsGPS_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = e.Source as MenuItem;
        var menu = menuItem?.Parent as ContextMenu;
        var listItem = menu?.PlacementTarget as ListViewItem;

        switch (listItem?.Content)
        {
        case PhysicsClusterAnalysisInfo clusterInfo:
            {
                var aabb = clusterInfo.AABBs[^1];
                var size = VRageMath.Vector3D.Round(aabb.Size, 0);
                var name = $"PhysicsCluster-{clusterInfo.ID}_({size.X}x{size.Y}x{size.Z})";
                var gps = FormatGPS(name, aabb.Center, 0);

                Clipboard.SetText(gps);
                break;
            }
        case CubeGridAnalysisInfo gridInfo:
            {
                var name = gridInfo.CustomNames.Length > 0 ? gridInfo.CustomNames[^1].Replace(':', '_') : $"{gridInfo.GridSize} Grid {gridInfo.EntityId}";
                var gps = FormatGPS(name, gridInfo.Positions[^1]);

                Clipboard.SetText(gps);
                break;
            }
        case CubeBlockAnalysisInfo blockInfo:
            {
                var name = blockInfo.CustomNames.Length > 0 ? blockInfo.CustomNames[^1].Replace(':', '_') : $"{blockInfo.BlockType.Name} {blockInfo.EntityId}";
                var gps = FormatGPS(name, blockInfo.Positions[^1]);

                Clipboard.SetText(gps);
                break;
            }
        }

        static string FormatGPS(string name, VRageMath.Vector3D coords, int decimals = 1)
        {
            coords = VRageMath.Vector3D.Round(coords, decimals);

            var ivCt = System.Globalization.CultureInfo.InvariantCulture;

            return $"GPS:{name}:{coords.X.ToString("R", ivCt)}:{coords.Y.ToString("R", ivCt)}:{coords.Z.ToString("R", ivCt)}:";
        }
    }
}
