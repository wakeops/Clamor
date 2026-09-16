using Clamor.Core.Models;
using Clamor.Core.Services;
using Clamor.Tests.TestSupport;
using Xunit;

namespace Clamor.Tests;

public class ProfileServiceTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsProfile()
    {
        using var dir = new TempDirectory();
        var service = new ProfileService(dir.Path);

        var profile = new Profile
        {
            Name = "Gaming",
            Clips = { new SoundClip { Name = "Airhorn", FilePath = "C:\\clips\\airhorn.wav", Volume = 0.8 } },
        };

        service.Save(profile);
        var loaded = service.Load("Gaming");

        Assert.Equal("Gaming", loaded.Name);
        Assert.Single(loaded.Clips);
        Assert.Equal("Airhorn", loaded.Clips[0].Name);
        Assert.Equal(0.8, loaded.Clips[0].Volume);
    }

    [Fact]
    public void LoadOrCreate_CreatesFile_WhenProfileIsMissing()
    {
        using var dir = new TempDirectory();
        var service = new ProfileService(dir.Path);

        Assert.False(service.Exists("New Profile"));
        var profile = service.LoadOrCreate("New Profile");

        Assert.Equal("New Profile", profile.Name);
        Assert.True(service.Exists("New Profile"));
    }

    [Fact]
    public void GetProfileNames_ReturnsSavedProfiles_SortedCaseInsensitive()
    {
        using var dir = new TempDirectory();
        var service = new ProfileService(dir.Path);

        service.Save(new Profile { Name = "zeta" });
        service.Save(new Profile { Name = "Alpha" });
        service.Save(new Profile { Name = "beta" });

        Assert.Equal(new[] { "Alpha", "beta", "zeta" }, service.GetProfileNames());
    }

    [Fact]
    public void Delete_RemovesProfileFile()
    {
        using var dir = new TempDirectory();
        var service = new ProfileService(dir.Path);
        service.Save(new Profile { Name = "Temp" });

        service.Delete("Temp");

        Assert.False(service.Exists("Temp"));
    }

    [Fact]
    public void Rename_MovesProfileToNewName()
    {
        using var dir = new TempDirectory();
        var service = new ProfileService(dir.Path);
        service.Save(new Profile { Name = "Old" });

        service.Rename("Old", "New");

        Assert.False(service.Exists("Old"));
        Assert.True(service.Exists("New"));
        Assert.Equal("New", service.Load("New").Name);
    }

    [Fact]
    public void Rename_Throws_WhenTargetAlreadyExists()
    {
        using var dir = new TempDirectory();
        var service = new ProfileService(dir.Path);
        service.Save(new Profile { Name = "A" });
        service.Save(new Profile { Name = "B" });

        Assert.Throws<InvalidOperationException>(() => service.Rename("A", "B"));
    }

    [Fact]
    public void Rename_Throws_WhenSourceIsMissing()
    {
        using var dir = new TempDirectory();
        var service = new ProfileService(dir.Path);

        Assert.Throws<FileNotFoundException>(() => service.Rename("Ghost", "New"));
    }
}
