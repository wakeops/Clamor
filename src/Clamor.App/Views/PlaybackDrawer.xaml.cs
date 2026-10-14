using System.Windows.Controls;
using System.Windows.Input;

namespace Clamor.App.Views;

public partial class PlaybackDrawer : UserControl
{
    public PlaybackDrawer()
    {
        InitializeComponent();
    }

    /// <summary>The delay boxes commit on focus loss; Enter commits too without leaving the box.</summary>
    private void DelayBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox box)
        {
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            box.SelectAll();
        }
    }
}
