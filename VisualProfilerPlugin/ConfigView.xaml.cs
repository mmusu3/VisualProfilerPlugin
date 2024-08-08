using System.Windows;
using System.Windows.Controls;

namespace VisualProfiler;

/// <summary>
/// Interaction logic for ConfigView.xaml
/// </summary>
partial class ConfigView : UserControl
{
    internal ConfigView(ConfigViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
    }

    void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this);
        ProfilerWindow.Open(mainWindow);
    }
}
