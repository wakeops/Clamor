using System.Windows;
using Clamor.App.Services;

namespace Clamor.App.Views;

public partial class VbCablePromptWindow : Window
{
    public VbCablePromptWindow()
    {
        InitializeComponent();
        WindowChromeFix.Apply(this);
    }

    private void Install_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void NotNow_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    public static bool ShowPrompt(Window? owner)
    {
        var dialog = new VbCablePromptWindow { Owner = owner };
        return dialog.ShowDialog() == true;
    }
}
