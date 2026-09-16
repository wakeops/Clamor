namespace Clamor.App.ViewModels;

/// <summary>UI-facing row for one entry in the main window's profile tab bar.</summary>
public sealed class ProfileTabViewModel : ViewModelBase
{
    private bool _isActive;
    private bool _isSelected;

    public ProfileTabViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    /// <summary>Whether this is the pinned "Default" profile — never draggable, and never a
    /// position other profiles can be dropped before.</summary>
    public bool IsDefault => Name == "Default";

    /// <summary>Whether this is the profile whose hotkeys are currently live.</summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }

    /// <summary>Whether this is the profile currently shown in the sound grid.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }
}
