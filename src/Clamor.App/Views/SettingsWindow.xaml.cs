using System.Windows;
using Clamor.App.Services;
using Clamor.App.ViewModels;

namespace Clamor.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        WindowChromeFix.Apply(this);
        DataContext = viewModel;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
