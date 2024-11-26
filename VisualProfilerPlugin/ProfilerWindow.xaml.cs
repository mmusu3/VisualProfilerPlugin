using System;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using VisualProfiler.Patches;

namespace VisualProfiler;

/// <summary>
/// Interaction logic for ProfilerWindow.xaml
/// </summary>
public partial class ProfilerWindow : Window, INotifyPropertyChanged
{
    public static bool IsOpen => window != null;

    static Thread? windowThread;
    static Dispatcher? dispatcher;
    static ProfilerWindow? window;

    const int maxRecordingSeconds = 60;
    const int maxRecordingFrames = 3600;

    Timer? recordingTimer;

    ProfilerEventsRecording? currentRecording;
    bool currentIsSaved;

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    bool IsRecordTimeValid()
    {
        if (recordTimeTypeBox == null)
            return false;

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

    public bool CanSave => currentRecording != null && !currentIsSaved;

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

    public bool AutoSaveRecording { get; set; }

    public bool CombineFrames
    {
        get => eventsGraph?.CombineFrames ?? false;
        set
        {
            if (eventsGraph != null)
                eventsGraph.CombineFrames = value;

            OnPropertyChanged(nameof(CombineFrames));
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
        else if (dispatcher != null)
        {
            dispatcher.BeginInvoke(CreateWindow, centerParent);
        }
    }

    static void ThreadStart(object? startObj)
    {
        CreateWindow((Point)startObj!);

        dispatcher = Dispatcher.CurrentDispatcher;

        Dispatcher.Run();
    }

    static void CreateWindow(Point centerParent)
    {
        window = new ProfilerWindow();
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = centerParent.X - window.Width / 2;
        window.Top = centerParent.Y - window.Height / 2;
        window.Show();
    }

    public ProfilerWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(CancelEventArgs args)
    {
        base.OnClosing(args);

        // TODO: Prompt if unsaved

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
            currentRecording = null;

            OnPropertyChanged(nameof(CanSave));

            startStopButton.Content = "Stop Recording";
            statisticsLabel.Content = "";
            frameTimesLabel.Content = "";
            objectCountsLabel.Content = "";
            outliersList.Items.Clear();
            physicsClustersList.ItemsSource = null;
            gridsList.ItemsSource = null;
            programmableBlocksList.ItemsSource = null;

            eventsGraph.SetRecordedEvents(null);

            GeneralStringCache.Clear();

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

    void OnRecordingStopped(ProfilerEventsRecording recording)
    {
        var session = Plugin.Instance.Torch.CurrentSession?.KeenSession;

        if (session != null)
            recording.SessionName = session.Name;

        if (AutoSaveRecording)
            Plugin.SaveRecording(recording);

        SetCurrentRecording(recording);
        currentIsSaved = false;

        OnPropertyChanged(nameof(CanSave));
    }

    void SaveButton_Click(object sender, RoutedEventArgs args)
    {
        if (currentRecording == null)
            return;

        if (Plugin.SaveRecordingDialog(currentRecording))
            currentIsSaved = true;

        OnPropertyChanged(nameof(CanSave));
    }

    void LoadButton_Click(object sender, RoutedEventArgs args)
    {
        var folderPath = Path.Combine(Plugin.Instance.StoragePath, "VisualProfiler", "Recordings");

        if (!Directory.Exists(folderPath))
            folderPath = null;

        var diag = new OpenFileDialog {
            InitialDirectory = folderPath,
            DefaultExt = ".prec",
            Filter = "Profiler Recordings (.prec)|*.prec"
        };

        bool? result = diag.ShowDialog();

        if (result is not true)
            return;

        LoadRecording(diag.FileName);
    }

    void LoadRecording(string path)
    {
        ProfilerEventsRecording? recording = null;

        try
        {
            using (var stream = File.Open(path, FileMode.Open))
            {
                using (var gzipStream = new GZipStream(stream, CompressionMode.Decompress))
                    recording = ProtoBuf.Serializer.Deserialize<ProfilerEventsRecording>(gzipStream);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to load profiler recording file.");
            // TODO: Msg box
        }

        if (recording == null)
            return;

        ProfilerHelper.RestoreRecordingObjectsAfterDeserialization(recording);

        SetCurrentRecording(recording);
        currentIsSaved = true;

        OnPropertyChanged(nameof(CanSave));
    }

    void SetCurrentRecording(ProfilerEventsRecording recording)
    {
        currentRecording = recording;

        startStopButton.Content = "Start New Recording";

        eventsGraph.SetRecordedEvents(recording);

        outliersList.Items.Clear();
        physicsClustersList.ItemsSource = null;
        gridsList.ItemsSource = null;
        programmableBlocksList.ItemsSource = null;

        var analysis = ProfilerHelper.AnalyzeRecording(recording);

        statisticsLabel.Content =
            $"""
            Session Name: {recording.SessionName}
            Recording Start Time: {recording.StartTime.ToLocalTime():yyyy/MM/d hh:mm:ss tt}
            Recorded {recording.NumFrames} frames
            """;

        frameTimesLabel.Content =
            $"""
            Frame Times:
                Min:    {Math.Round(analysis.FrameTimes.Min, 2)}ms
                Max:    {Math.Round(analysis.FrameTimes.Max, 2)}ms
                Mean:   {Math.Round(analysis.FrameTimes.Mean, 2)}ms
                StdDev: {Math.Round(analysis.FrameTimes.StdDev, 2)}ms
            """;

        objectCountsLabel.Content =
            $"""
            Object Counts:
                Clusters: {analysis.PhysicsClusters.Length}
                Grids: {analysis.Grids.Length}
                Programable Blocks: {analysis.ProgrammableBlocks.Length}
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

        physicsClustersList.ContextMenu = clustersContextMenu;
        physicsClustersList.ItemsSource = analysis.PhysicsClusters;
        physicsClustersList.Items.SortDescriptions.Clear();
        physicsClustersList.Items.SortDescriptions.Add(new SortDescription(nameof(PhysicsClusterAnalysisInfo.TotalTime), ListSortDirection.Descending));

        copyLastPosAsGPS = new MenuItem { Header = "Copy Last Position as GPS" };
        copyLastPosAsGPS.Click += CopyLastPosAsGPS_Click;

        var gridsContextMenu = new ContextMenu();
        // TODO: Copy EntityID
        gridsContextMenu.Items.Add(copyLastPosAsGPS);

        gridsList.ContextMenu = gridsContextMenu;
        gridsList.ItemsSource = analysis.Grids;
        gridsList.Items.SortDescriptions.Clear();
        gridsList.Items.SortDescriptions.Add(new SortDescription(nameof(CubeGridAnalysisInfo.TotalTime), ListSortDirection.Descending));
        gridsList.Items.Filter = FilterCubeGridItem;

        copyLastPosAsGPS = new MenuItem { Header = "Copy Last Position as GPS" };
        copyLastPosAsGPS.Click += CopyLastPosAsGPS_Click;

        var blocksContextMenu = new ContextMenu();
        blocksContextMenu.Items.Add(copyLastPosAsGPS);

        programmableBlocksList.ContextMenu = blocksContextMenu;
        programmableBlocksList.ItemsSource = analysis.ProgrammableBlocks;
        programmableBlocksList.Items.SortDescriptions.Clear();
        programmableBlocksList.Items.SortDescriptions.Add(new SortDescription(nameof(CubeBlockAnalysisInfo.TotalTime), ListSortDirection.Descending));
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

    #region List expanders

    void RecordingInfoExpander_Expanded(object sender, RoutedEventArgs e)
    {
    }

    void RecordingInfoExpander_Collapsed(object sender, RoutedEventArgs e)
    {
    }

    void OutlierFramesExpander_Expanded(object sender, RoutedEventArgs e)
    {
        if (outliersList == null)
            return;

        var h1 = physicsClustersRow.Height;
        var h2 = cubeGridsRow.Height;
        var h3 = programmableBlocksRow.Height;

        double height = 0;
        int num = 0;

        if (h1.IsAbsolute)
        {
            height += h1.Value;
            num++;
        }

        if (h2.IsAbsolute)
        {
            height += h2.Value;
            num++;
        }

        if (h3.IsAbsolute)
        {
            height += h3.Value;
            num++;
        }

        if (num > 1)
        {
            if (h1.IsAbsolute)
                physicsClustersRow.Height = new GridLength(h1.Value / height, GridUnitType.Star);

            if (h2.IsAbsolute)
                cubeGridsRow.Height = new GridLength(h2.Value / height, GridUnitType.Star);

            if (h3.IsAbsolute)
                programmableBlocksRow.Height = new GridLength(h3.Value / height, GridUnitType.Star);
        }

        var expander = (Expander)sender;

        outlierFramesRow.Height = new GridLength(expander.ActualHeight + outliersList.ActualHeight + 4);
        outlierFramesRow.MinHeight = 60;
        outlierListSplitter.IsEnabled = true;
    }

    void OutlierFramesExpander_Collapsed(object sender, RoutedEventArgs e)
    {
        outlierFramesRow.Height = GridLength.Auto;
        outlierFramesRow.MinHeight = 27;
        outlierListSplitter.IsEnabled = false;
    }

    void PhysicsClustersExpander_Expanded(object sender, RoutedEventArgs e)
    {
        if (physicsClustersList == null)
            return;

        var h1 = cubeGridsRow.Height;
        var h2 = programmableBlocksRow.Height;

        double height = 0;
        int num = 0;

        if (h1.IsAbsolute)
        {
            height += h1.Value;
            num++;
        }

        if (h2.IsAbsolute)
        {
            height += h2.Value;
            num++;
        }

        if (num > 1)
        {
            if (h1.IsAbsolute)
                cubeGridsRow.Height = new GridLength(h1.Value / height, GridUnitType.Star);

            if (h2.IsAbsolute)
                programmableBlocksRow.Height = new GridLength(h2.Value / height, GridUnitType.Star);
        }

        var expander = (Expander)sender;

        physicsClustersRow.Height = new GridLength(expander.ActualHeight + physicsClustersList.ActualHeight + 4);
        physicsClustersRow.MinHeight = 80;
        clusterListSplitter.IsEnabled = true;
    }

    void PhysicsClustersExpander_Collapsed(object sender, RoutedEventArgs e)
    {
        physicsClustersRow.Height = GridLength.Auto;
        physicsClustersRow.MinHeight = 27;
        clusterListSplitter.IsEnabled = false;
    }

    void CubeGridsExpander_Expanded(object sender, RoutedEventArgs e)
    {
        if (gridsList == null)
            return;

        if (programmableBlocksRow.Height.IsAbsolute)
            programmableBlocksRow.Height = new GridLength(1, GridUnitType.Star);

        var expander = (Expander)sender;

        cubeGridsRow.Height = new GridLength(expander.ActualHeight + gridListFilterTextBox.ActualHeight + gridsList.ActualHeight + 4);
        cubeGridsRow.MinHeight = 110;
        gridListSplitter.IsEnabled = true;
    }

    void CubeGridsExpander_Collapsed(object sender, RoutedEventArgs e)
    {
        cubeGridsRow.Height = GridLength.Auto;
        cubeGridsRow.MinHeight = 27;
        gridListSplitter.IsEnabled = false;
    }

    void ProgrammableBlocksExpander_Expanded(object sender, RoutedEventArgs e)
    {
        if (programmableBlocksList == null)
            return;

        var expander = (Expander)sender;

        programmableBlocksRow.MinHeight = 80;

        if (outlierFramesRow.Height.IsAbsolute || physicsClustersRow.Height.IsAbsolute || cubeGridsRow.Height.IsAbsolute)
            programmableBlocksRow.Height = new GridLength(expander.ActualHeight + programmableBlocksList.ActualHeight + 4);
        else
            programmableBlocksRow.Height = new GridLength(1, GridUnitType.Star);

        gridListSplitter.IsEnabled = gridListExpander.IsExpanded;
    }

    void ProgrammableBlocksExpander_Collapsed(object sender, RoutedEventArgs e)
    {
        programmableBlocksRow.Height = GridLength.Auto;

        if (outlierFramesRow.Height.IsStar)
            outlierFramesRow.Height = new GridLength(outlierFramesRow.ActualHeight, GridUnitType.Pixel);

        if (physicsClustersRow.Height.IsStar)
            physicsClustersRow.Height = new GridLength(physicsClustersRow.ActualHeight, GridUnitType.Pixel);

        if (cubeGridsRow.Height.IsAbsolute)
            cubeGridsRow.Height = new GridLength(1, GridUnitType.Star);

        programmableBlocksRow.MinHeight = 27;
        gridListSplitter.IsEnabled = false;
    }

    #endregion

    #region Physics cluster list sorting

    void PhysicsClustersListHeader_Click(object sender, RoutedEventArgs args)
    {
        if (args.OriginalSource is not GridViewColumnHeader header)
            return;

        string propName = "";
        var defaultDir = ListSortDirection.Ascending;
        IComparer[]? comparers = null;

        switch (header.Name)
        {
        case nameof(physicsClusterTotalTimeColumn):
            propName = nameof(PhysicsClusterAnalysisInfo.TotalTime);
            defaultDir = ListSortDirection.Descending;
            break;
        case nameof(physicsClusterAverageTimeColumn):
            propName = nameof(PhysicsClusterAnalysisInfo.AverageTimePerFrame);
            defaultDir = ListSortDirection.Descending;
            break;
        case nameof(physicsClusterCountedFramesColumn):
            propName = nameof(PhysicsClusterAnalysisInfo.NumFramesCounted);
            defaultDir = ListSortDirection.Descending;
            break;
        case nameof(physicsClusterIdColumn):
            propName = nameof(PhysicsClusterAnalysisInfo.ID);
            break;
        case nameof(physicsClusterNumObjectsColumn):
            propName = nameof(PhysicsClusterAnalysisInfo.ObjectCountsForColumn);
            break;
        case nameof(physicsClusterNumActiveObjectsColumn):
            propName = nameof(PhysicsClusterAnalysisInfo.ActiveObjectCountsForColumn);
            break;
        case nameof(physicsClusterNumCharactersColumn):
            propName = nameof(PhysicsClusterAnalysisInfo.CharacterCountsForColumn);
            comparers = CubeGridOwnerIDsComparer.Instances;
            break;
        case nameof(physicsClusterSizeColumn):
            propName = nameof(PhysicsClusterAnalysisInfo.SizeForColumn);
            break;
        case nameof(physicsClusterPositionColumn):
            propName = nameof(PhysicsClusterAnalysisInfo.AveragePositionForColumn);
            break;
        default:
            break;
        }

        if (string.IsNullOrEmpty(propName))
            return;

        var listCollectionView = (ListCollectionView)CollectionViewSource.GetDefaultView(physicsClustersList.ItemsSource);
        ListSortDirection sortDir;

        if (physicsClustersListCurrentSortProp == propName)
            sortDir = (ListSortDirection)((int)physicsClustersListCurrentSortDir ^ 1);
        else
            sortDir = defaultDir;

        physicsClustersListCurrentSortProp = propName;
        physicsClustersListCurrentSortDir = sortDir;

        if (comparers != null)
        {
            listCollectionView.CustomSort = comparers[(int)sortDir]; // CustomSort setter clears SortDescriptions
        }
        else
        {
            physicsClustersList.Items.SortDescriptions.Clear();
            physicsClustersList.Items.SortDescriptions.Add(new(propName, sortDir));
        }
    }

    string physicsClustersListCurrentSortProp = "";
    ListSortDirection physicsClustersListCurrentSortDir;

    #endregion

    #region Grid list sorting

    void CubeGridsListHeader_Click(object sender, RoutedEventArgs args)
    {
        if (args.OriginalSource is not GridViewColumnHeader header)
            return;

        string propName = "";
        var defaultDir = ListSortDirection.Ascending;
        IComparer[]? comparers = null;

        switch (header.Name)
        {
        case nameof(gridTotalTimeColumn):
            propName = nameof(CubeGridAnalysisInfo.TotalTime);
            defaultDir = ListSortDirection.Descending;
            break;
        case nameof(gridAverageTimeColumn):
            propName = nameof(CubeGridAnalysisInfo.AverageTimePerFrame);
            defaultDir = ListSortDirection.Descending;
            break;
        case nameof(gridCountedFramesColumn):
            propName = nameof(CubeGridAnalysisInfo.NumFramesCounted);
            defaultDir = ListSortDirection.Descending;
            break;
        case nameof(gridTypeColumn):
            propName = nameof(CubeGridAnalysisInfo.GridTypeForColumn);
            break;
        case nameof(gridEntityIdColumn):
            propName = nameof(CubeGridAnalysisInfo.EntityId);
            break;
        case nameof(gridNamesColumn):
            propName = nameof(CubeGridAnalysisInfo.NamesForColumn);
            break;
        case nameof(gridOwnerIdsColumn):
            propName = nameof(CubeGridAnalysisInfo.OwnerIDsForColumn);
            comparers = CubeGridOwnerIDsComparer.Instances;
            break;
        case nameof(gridOwnerNamesColumn):
            propName = nameof(CubeGridAnalysisInfo.OwnerNamesForColumn);
            break;
        case nameof(gridBlockCountsColumn):
            propName = nameof(CubeGridAnalysisInfo.BlockCountsForColumn);
            defaultDir = ListSortDirection.Descending;
            comparers = CubeGridBlockCountsComparer.Instances;
            break;
        case nameof(gridPCUsColumn):
            propName = nameof(CubeGridAnalysisInfo.PCUsForColumn);
            defaultDir = ListSortDirection.Descending;
            comparers = CubeGridPCUsComparer.Instances;
            break;
        case nameof(gridSizesColumn):
            propName = nameof(CubeGridAnalysisInfo.SizesForColumn);
            defaultDir = ListSortDirection.Descending;
            comparers = CubeGridSizesComparer.Instances;
            break;
        case nameof(gridAveragePositionColumn):
            propName = nameof(CubeGridAnalysisInfo.AveragePositionForColumn);
            break;
        case nameof(gridAverageSpeedColumn):
            propName = nameof(CubeGridAnalysisInfo.AverageSpeedForColumn);
            comparers = CubeGridAverageSpeedComparer.Instances;
            defaultDir = ListSortDirection.Descending;
            break;
        case nameof(gridIsPoweredColumn):
            propName = nameof(CubeGridAnalysisInfo.IsPoweredForColumn);
            break;
        default:
            break;
        }

        if (string.IsNullOrEmpty(propName))
            return;

        var listCollectionView = (ListCollectionView)CollectionViewSource.GetDefaultView(gridsList.ItemsSource);
        ListSortDirection sortDir;

        if (cubeGridsListCurrentSortProp == propName)
            sortDir = (ListSortDirection)((int)cubeGridsListCurrentSortDir ^ 1);
        else
            sortDir = defaultDir;

        cubeGridsListCurrentSortProp = propName;
        cubeGridsListCurrentSortDir = sortDir;

        if (comparers != null)
        {
            listCollectionView.CustomSort = comparers[(int)sortDir]; // CustomSort setter clears SortDescriptions
        }
        else
        {
            gridsList.Items.SortDescriptions.Clear();
            gridsList.Items.SortDescriptions.Add(new(propName, sortDir));
        }
    }

    string cubeGridsListCurrentSortProp = "";
    ListSortDirection cubeGridsListCurrentSortDir;

    class CubeGridOwnerIDsComparer(ListSortDirection sortDirection) : IComparer
    {
        public static readonly CubeGridOwnerIDsComparer Ascending = new(ListSortDirection.Ascending);
        public static readonly CubeGridOwnerIDsComparer Descending = new(ListSortDirection.Descending);
        public static readonly CubeGridOwnerIDsComparer[] Instances = [Ascending, Descending];

        public ListSortDirection SortDirection = sortDirection;

        public int Compare(object? x, object? y)
        {
            var gridX = x as CubeGridAnalysisInfo;
            var gridY = y as CubeGridAnalysisInfo;

            if (gridX == null || gridY == null)
                return 0;

            var ownersX = gridX.Owners;
            var ownersY = gridY.Owners;

            if (ownersX.Length == 1)
            {
                if (ownersY.Length == 1)
                {
                    if (SortDirection == ListSortDirection.Ascending)
                        return ownersX[0].ID.CompareTo(ownersY[0].ID);
                    else
                        return ownersY[0].ID.CompareTo(ownersX[0].ID);
                }

                return -1;
            }
            else
            {
                if (ownersY.Length == 1)
                    return 1;

                if (SortDirection == ListSortDirection.Ascending)
                    return ownersX.Length.CompareTo(ownersY.Length);
                else
                    return ownersY.Length.CompareTo(ownersX.Length);
            }
        }
    }

    class CubeGridBlockCountsComparer(ListSortDirection sortDirection) : IComparer
    {
        public static readonly CubeGridBlockCountsComparer Ascending = new(ListSortDirection.Ascending);
        public static readonly CubeGridBlockCountsComparer Descending = new(ListSortDirection.Descending);
        public static readonly CubeGridBlockCountsComparer[] Instances = [Ascending, Descending];

        public ListSortDirection SortDirection = sortDirection;

        public int Compare(object? x, object? y)
        {
            var gridX = x as CubeGridAnalysisInfo;
            var gridY = y as CubeGridAnalysisInfo;

            if (gridX == null || gridY == null)
                return 0;

            var countsX = gridX.BlockCounts;
            var countsY = gridY.BlockCounts;

            if (countsX.Length == 1)
            {
                if (countsY.Length == 1)
                {
                    if (SortDirection == ListSortDirection.Ascending)
                        return countsX[0].CompareTo(countsY[0]);
                    else
                        return countsY[0].CompareTo(countsX[0]);
                }

                return -1;
            }
            else
            {
                if (countsY.Length == 1)
                    return 1;

                if (SortDirection == ListSortDirection.Ascending)
                    return countsX.Length.CompareTo(countsY.Length);
                else
                    return countsY.Length.CompareTo(countsX.Length);
            }
        }
    }

    class CubeGridPCUsComparer(ListSortDirection sortDirection) : IComparer
    {
        public static readonly CubeGridPCUsComparer Ascending = new(ListSortDirection.Ascending);
        public static readonly CubeGridPCUsComparer Descending = new(ListSortDirection.Descending);
        public static readonly CubeGridPCUsComparer[] Instances = [Ascending, Descending];

        public ListSortDirection SortDirection = sortDirection;

        public int Compare(object? x, object? y)
        {
            var gridX = x as CubeGridAnalysisInfo;
            var gridY = y as CubeGridAnalysisInfo;

            if (gridX == null || gridY == null)
                return 0;

            var countsX = gridX.PCUs;
            var countsY = gridY.PCUs;

            if (countsX.Length == 1)
            {
                if (countsY.Length == 1)
                {
                    if (SortDirection == ListSortDirection.Ascending)
                        return countsX[0].CompareTo(countsY[0]);
                    else
                        return countsY[0].CompareTo(countsX[0]);
                }

                return -1;
            }
            else
            {
                if (countsY.Length == 1)
                    return 1;

                if (SortDirection == ListSortDirection.Ascending)
                    return countsX.Length.CompareTo(countsY.Length);
                else
                    return countsY.Length.CompareTo(countsX.Length);
            }
        }
    }

    class CubeGridSizesComparer(ListSortDirection sortDirection) : IComparer
    {
        public static readonly CubeGridSizesComparer Ascending = new(ListSortDirection.Ascending);
        public static readonly CubeGridSizesComparer Descending = new(ListSortDirection.Descending);
        public static readonly CubeGridSizesComparer[] Instances = [Ascending, Descending];

        public ListSortDirection SortDirection = sortDirection;

        public int Compare(object? x, object? y)
        {
            var gridX = x as CubeGridAnalysisInfo;
            var gridY = y as CubeGridAnalysisInfo;

            if (gridX == null || gridY == null)
                return 0;

            var sizesX = gridX.Sizes;
            var sizesY = gridY.Sizes;

            if (sizesX.Length == 1)
            {
                if (sizesY.Length == 1)
                {
                    if (SortDirection == ListSortDirection.Ascending)
                        return sizesX[0].Volume().CompareTo(sizesY[0].Volume());
                    else
                        return sizesY[0].Volume().CompareTo(sizesX[0].Volume());
                }

                return -1;
            }
            else
            {
                if (sizesY.Length == 1)
                    return 1;

                if (SortDirection == ListSortDirection.Ascending)
                    return sizesX.Length.CompareTo(sizesY.Length);
                else
                    return sizesY.Length.CompareTo(sizesX.Length);
            }
        }
    }

    class CubeGridAverageSpeedComparer(ListSortDirection sortDirection) : IComparer
    {
        public static readonly CubeGridAverageSpeedComparer Ascending = new(ListSortDirection.Ascending);
        public static readonly CubeGridAverageSpeedComparer Descending = new(ListSortDirection.Descending);
        public static readonly CubeGridAverageSpeedComparer[] Instances = [Ascending, Descending];

        public ListSortDirection SortDirection = sortDirection;

        public int Compare(object? x, object? y)
        {
            var gridX = x as CubeGridAnalysisInfo;
            var gridY = y as CubeGridAnalysisInfo;

            if (gridX == null || gridY == null)
                return 0;

            var speedX = gridX.AverageSpeed;
            var speedY = gridY.AverageSpeed;

            if (SortDirection == ListSortDirection.Ascending)
                return speedX.CompareTo(speedY);
            else
                return speedY.CompareTo(speedX);
        }
    }

    #endregion

    bool FilterCubeGridItem(object obj)
    {
        if (obj is not CubeGridAnalysisInfo gridInfo)
            return false;

        var filter = gridListFilterTextBox.Text;

        if (string.IsNullOrWhiteSpace(filter))
            return true;

        foreach (var name in gridInfo.Names)
        {
            if (name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var owner in gridInfo.Owners)
        {
            if (owner.Name != null && owner.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (gridInfo.IsStatic != false)
        {
            if ("station".Contains(filter, StringComparison.OrdinalIgnoreCase))
                return true;

            if ("static".Contains(filter, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        switch (gridInfo.GridSize)
        {
        case VRage.Game.MyCubeSize.Large:
            if ("large".Contains(filter, StringComparison.OrdinalIgnoreCase))
                return true;
            break;
        case VRage.Game.MyCubeSize.Small:
            if ("small".Contains(filter, StringComparison.OrdinalIgnoreCase))
                return true;
            break;
        }

        if (gridInfo.EntityId.ToString().Contains(filter, StringComparison.Ordinal))
            return true;

        return false;
    }

    void GridListFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CollectionViewSource.GetDefaultView(gridsList.ItemsSource).Refresh();
    }

    #region PB list sorting

    void ProgBlocksListHeader_Click(object sender, RoutedEventArgs args)
    {
        if (args.OriginalSource is not GridViewColumnHeader header)
            return;

        string propName = "";
        var defaultDir = ListSortDirection.Ascending;
        IComparer[]? comparers = null;

        switch (header.Name)
        {
        case nameof(blockTotalTimeColumn):
            propName = nameof(CubeBlockAnalysisInfo.TotalTime);
            defaultDir = ListSortDirection.Descending;
            break;
        case nameof(blockAverageTimeColumn):
            propName = nameof(CubeBlockAnalysisInfo.AverageTimePerFrame);
            defaultDir = ListSortDirection.Descending;
            break;
        case nameof(blockCountedFramesColumn):
            propName = nameof(CubeBlockAnalysisInfo.NumFramesCounted);
            defaultDir = ListSortDirection.Descending;
            break;
        case nameof(blockGridSizeColumn):
            propName = nameof(CubeGridAnalysisInfo.GridSize);
            break;
        case nameof(blockEntityIdColumn):
            propName = nameof(CubeBlockAnalysisInfo.EntityId);
            break;
        case nameof(blockGridIdColumn):
            propName = nameof(CubeBlockAnalysisInfo.GridIdsForColumn);
            comparers = CubeBlockGridIDsComparer.Instances;
            break;
        case nameof(blockCustomNamesColumn):
            propName = nameof(CubeBlockAnalysisInfo.CustomNamesForColumn);
            break;
        case nameof(blockOwnerIdsColumn):
            propName = nameof(CubeBlockAnalysisInfo.OwnerIDsForColumn);
            comparers = CubeBlockOwnerIDsComparer.Instances;
            break;
        case nameof(blockOwnerNamesColumn):
            propName = nameof(CubeBlockAnalysisInfo.OwnerNamesForColumn);
            break;
        case nameof(blockAveragePositionColumn):
            propName = nameof(CubeBlockAnalysisInfo.AveragePositionForColumn);
            break;
        default:
            break;
        }

        if (string.IsNullOrEmpty(propName))
            return;

        var listCollectionView = (ListCollectionView)CollectionViewSource.GetDefaultView(programmableBlocksList.ItemsSource);
        ListSortDirection sortDir;

        if (cubeBlocksListCurrentSortProp == propName)
            sortDir = (ListSortDirection)((int)cubeBlocksListCurrentSortDir ^ 1);
        else
            sortDir = defaultDir;

        cubeBlocksListCurrentSortProp = propName;
        cubeBlocksListCurrentSortDir = sortDir;

        if (comparers != null)
        {
            listCollectionView.CustomSort = comparers[(int)sortDir]; // CustomSort setter clears SortDescriptions
        }
        else
        {
            programmableBlocksList.Items.SortDescriptions.Clear();
            programmableBlocksList.Items.SortDescriptions.Add(new(propName, sortDir));
        }
    }

    string cubeBlocksListCurrentSortProp = "";
    ListSortDirection cubeBlocksListCurrentSortDir;

    class CubeBlockGridIDsComparer(ListSortDirection sortDirection) : IComparer
    {
        public static readonly CubeBlockGridIDsComparer Ascending = new(ListSortDirection.Ascending);
        public static readonly CubeBlockGridIDsComparer Descending = new(ListSortDirection.Descending);
        public static readonly CubeBlockGridIDsComparer[] Instances = [Ascending, Descending];

        public ListSortDirection SortDirection = sortDirection;

        public int Compare(object? x, object? y)
        {
            var blockX = x as CubeBlockAnalysisInfo;
            var blockY = y as CubeBlockAnalysisInfo;

            if (blockX == null || blockY == null)
                return 0;

            var gridIdsX = blockX.GridIds;
            var gridIdsY = blockY.GridIds;

            if (gridIdsX.Length == 1)
            {
                if (gridIdsY.Length == 1)
                {
                    if (SortDirection == ListSortDirection.Ascending)
                        return gridIdsX[0].CompareTo(gridIdsY[0]);
                    else
                        return gridIdsY[0].CompareTo(gridIdsX[0]);
                }

                return -1;
            }
            else
            {
                if (gridIdsY.Length == 1)
                    return 1;

                if (SortDirection == ListSortDirection.Ascending)
                    return gridIdsX.Length.CompareTo(gridIdsY.Length);
                else
                    return gridIdsY.Length.CompareTo(gridIdsX.Length);
            }
        }
    }

    class CubeBlockOwnerIDsComparer(ListSortDirection sortDirection) : IComparer
    {
        public static readonly CubeBlockOwnerIDsComparer Ascending = new(ListSortDirection.Ascending);
        public static readonly CubeBlockOwnerIDsComparer Descending = new(ListSortDirection.Descending);
        public static readonly CubeBlockOwnerIDsComparer[] Instances = [Ascending, Descending];

        public ListSortDirection SortDirection = sortDirection;

        public int Compare(object? x, object? y)
        {
            var blockX = x as CubeBlockAnalysisInfo;
            var blockY = y as CubeBlockAnalysisInfo;

            if (blockX == null || blockY == null)
                return 0;

            var ownersX = blockX.Owners;
            var ownersY = blockY.Owners;

            if (ownersX.Length == 1)
            {
                if (ownersY.Length == 1)
                {
                    if (SortDirection == ListSortDirection.Ascending)
                        return ownersX[0].ID.CompareTo(ownersY[0].ID);
                    else
                        return ownersY[0].ID.CompareTo(ownersX[0].ID);
                }

                return -1;
            }
            else
            {
                if (ownersY.Length == 1)
                    return 1;

                if (SortDirection == ListSortDirection.Ascending)
                    return ownersX.Length.CompareTo(ownersY.Length);
                else
                    return ownersY.Length.CompareTo(ownersX.Length);
            }
        }
    }

    #endregion

    void ResetViewButton_Click(object sender, RoutedEventArgs args)
    {
        eventsGraph.ResetZoom();
    }

    void TimerCompleted(object? state)
    {
        if (!Profiler.IsRecordingEvents)
            return;

        var recording = Profiler.StopEventRecording();

        ClearTimer();
        Dispatcher.BeginInvoke(OnRecordingStopped, recording);
    }

    void RecordingFramesCompleted(ProfilerEventsRecording recording)
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
        if (e.Source is not MenuItem menuItem)
            return;

        if (menuItem.Parent is not ContextMenu menu)
            return;

        object? selectedItem;

        if (menu.PlacementTarget is ListViewItem lvi)
            selectedItem = lvi.Content;
        else if (menu.PlacementTarget is ListView lv)
            selectedItem = lv.SelectedItem is ListViewItem { Content: var c } ? c : lv.SelectedItem;
        else
            return;

        switch (selectedItem)
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
                var name = gridInfo.Names.Length > 0 ? gridInfo.Names[^1].Replace(':', '_') : $"{gridInfo.GridSize} Grid {gridInfo.EntityId}";
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
