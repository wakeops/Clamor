using Microsoft.Win32;

namespace Clamor.App.Services;

public sealed class FileDialogService : IFileDialogService
{
    public IReadOnlyList<string> OpenAudioFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add Sound Clips",
            Filter = "Audio Files (*.mp3;*.wav)|*.mp3;*.wav|All Files (*.*)|*.*",
            Multiselect = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileNames : Array.Empty<string>();
    }
}
