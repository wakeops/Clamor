using System.Windows;

namespace Clamor.App.Views;

public partial class InputDialog : Window
{
    public InputDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    public static string? Prompt(Window? owner, string title, string prompt, string defaultValue = "")
    {
        var dialog = new InputDialog { Title = title, Owner = owner };
        dialog.PromptText.Text = prompt;
        dialog.InputBox.Text = defaultValue;
        return dialog.ShowDialog() == true ? dialog.InputBox.Text : null;
    }
}
