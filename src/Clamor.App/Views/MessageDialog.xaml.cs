using System.Windows;
using Clamor.App.Services;

namespace Clamor.App.Views;

public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
        WindowChromeFix.Apply(this);
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void SecondaryButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    public static void ShowInfo(Window? owner, string title, string message, string okText = "OK")
    {
        var dialog = new MessageDialog { Title = title, Owner = owner };
        dialog.MessageText.Text = message;
        dialog.PrimaryButton.Content = okText;
        dialog.ShowDialog();
    }

    public static bool ShowConfirm(Window? owner, string title, string message, string yesText = "Yes", string noText = "Cancel")
    {
        var dialog = new MessageDialog { Title = title, Owner = owner };
        dialog.MessageText.Text = message;
        dialog.PrimaryButton.Content = yesText;
        dialog.SecondaryButton.Content = noText;
        dialog.SecondaryButton.Visibility = Visibility.Visible;
        return dialog.ShowDialog() == true;
    }
}
