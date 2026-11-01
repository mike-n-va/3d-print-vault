using Avalonia.Controls;
using DPV.UI.ViewModels;

namespace DPV.UI;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public MainWindow()
        : this(new ShellViewModel(new DashboardViewModel()))
    {
    }
}