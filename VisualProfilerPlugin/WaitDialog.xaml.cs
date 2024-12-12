using System.ComponentModel;
using System.Windows;

namespace VisualProfiler;

public partial class WaitDialog : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Message
    {
        get => message;
        set
        {
            message = value;
            PropertyChanged?.Invoke(this, new(nameof(Message)));
        }
    }
    string message = "";

    public WaitDialog()
    {
        InitializeComponent();
    }
}
