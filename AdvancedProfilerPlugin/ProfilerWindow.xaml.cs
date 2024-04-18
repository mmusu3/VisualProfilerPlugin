using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace AdvancedProfiler;

/// <summary>
/// Interaction logic for ProfilerWindow.xaml
/// </summary>
public partial class ProfilerWindow : Window
{
    static Thread? windowThread;
    static ProfilerWindow? window;

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

    static void ThreadStart(object startObj)
    {
        var centerParent = (Point)startObj;

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

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        window = null;
    }

    void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (Profiler.IsRecordingEvents)
        {
            startStopButton.Content = "Start Profiling";
            Profiler.StopEventRecording();
            eventsGraph.ResetZoom();
        }
        else
        {
            startStopButton.Content = "Stop Profiling";
            Profiler.StartEventRecording();
            eventsGraph.InvalidateVisual();
        }
    }

    void ResetViewButton_Click(object sender, RoutedEventArgs e)
    {
        eventsGraph.ResetZoom();
    }
}
