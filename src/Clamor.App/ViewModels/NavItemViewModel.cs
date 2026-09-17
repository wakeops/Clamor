namespace Clamor.App.ViewModels;

/// <summary>One entry in the left nav rail — icon + label stacked, with a fixed selection state
/// for now since there's only one destination to be on.</summary>
public sealed class NavItemViewModel
{
    public NavItemViewModel(string name, string icon, bool isActive)
    {
        Name = name;
        Icon = icon;
        IsActive = isActive;
    }

    public string Name { get; }

    /// <summary>A Segoe MDL2 Assets glyph.</summary>
    public string Icon { get; }

    public bool IsActive { get; }
}
