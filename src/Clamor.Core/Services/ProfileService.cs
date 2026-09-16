using System.Text.Json;
using Clamor.Core.Models;

namespace Clamor.Core.Services;

public sealed class ProfileService : IProfileService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public string ProfilesDirectory { get; }

    public ProfileService(string? appDataDirectory = null)
    {
        var root = appDataDirectory ?? SettingsService.DefaultAppDataDirectory();
        ProfilesDirectory = Path.Combine(root, "profiles");
        Directory.CreateDirectory(ProfilesDirectory);
    }

    public IReadOnlyList<string> GetProfileNames()
    {
        return Directory.EnumerateFiles(ProfilesDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool Exists(string name) => File.Exists(GetPath(name));

    public Profile Load(string name)
    {
        var path = GetPath(name);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Profile>(json, SerializerOptions) ?? new Profile { Name = name };
    }

    public Profile LoadOrCreate(string name)
    {
        if (Exists(name))
        {
            return Load(name);
        }

        var profile = new Profile { Name = name };
        Save(profile);
        return profile;
    }

    public void Save(Profile profile)
    {
        var json = JsonSerializer.Serialize(profile, SerializerOptions);
        File.WriteAllText(GetPath(profile.Name), json);
    }

    public void Delete(string name)
    {
        var path = GetPath(name);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void Rename(string oldName, string newName)
    {
        if (!Exists(oldName))
        {
            throw new FileNotFoundException($"Profile '{oldName}' does not exist.");
        }

        if (Exists(newName))
        {
            throw new InvalidOperationException($"Profile '{newName}' already exists.");
        }

        var profile = Load(oldName);
        profile.Name = newName;
        Save(profile);
        Delete(oldName);
    }

    private string GetPath(string name) => Path.Combine(ProfilesDirectory, $"{SanitizeFileName(name)}.json");

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "profile" : sanitized;
    }
}
