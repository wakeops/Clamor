namespace Clamor.App.Services;

public interface IFileDialogService
{
    /// <summary>Prompts the user to pick one or more audio files; returns an empty list if cancelled.</summary>
    IReadOnlyList<string> OpenAudioFiles();
}
