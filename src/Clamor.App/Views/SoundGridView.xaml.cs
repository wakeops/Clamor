using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clamor.App.ViewModels;

namespace Clamor.App.Views;

public partial class SoundGridView : UserControl
{
    public SoundGridView()
    {
        InitializeComponent();
    }

    private void ClipName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2)
        {
            return;
        }

        // Stop the double-click from bubbling into the sound button's own Click (which plays
        // the clip) — renaming and playing should never fire off the same gesture.
        e.Handled = true;

        if (sender is not FrameworkElement { DataContext: SoundClipViewModel clipVm } ||
            DataContext is not MainViewModel mainViewModel)
        {
            return;
        }

        var newName = InputDialog.Prompt(Window.GetWindow(this), "Rename Clip", "Clip name:", clipVm.Name);
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        mainViewModel.RenameClip(clipVm, newName.Trim());
    }
}
