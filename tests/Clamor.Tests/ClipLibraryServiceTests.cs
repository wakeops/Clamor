using Clamor.Core.Services;
using Clamor.Tests.TestSupport;
using Xunit;

namespace Clamor.Tests;

public class ClipLibraryServiceTests
{
    private static string CreateDummyAudioFile(string dir, string name = "clip.wav")
    {
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, Array.Empty<byte>());
        return path;
    }

    [Fact]
    public void AddClip_Throws_WhenFileIsMissing()
    {
        using var dir = new TempDirectory();
        var service = new ClipLibraryService(new ProfileService(dir.Path));

        Assert.Throws<FileNotFoundException>(() => service.AddClip(Path.Combine(dir.Path, "missing.wav")));
    }

    [Fact]
    public void AddClip_AddsAndPersistsClip()
    {
        using var dir = new TempDirectory();
        var profileService = new ProfileService(dir.Path);
        var service = new ClipLibraryService(profileService);
        var filePath = CreateDummyAudioFile(dir.Path);

        var clip = service.AddClip(filePath, "Airhorn");

        Assert.Equal("Airhorn", clip.Name);
        Assert.Single(service.CurrentProfile.Clips);

        var reloaded = profileService.Load("Default");
        Assert.Single(reloaded.Clips);
        Assert.Equal("Airhorn", reloaded.Clips[0].Name);
    }

    [Fact]
    public void AddClip_AssignsIncrementingSortOrder()
    {
        using var dir = new TempDirectory();
        var service = new ClipLibraryService(new ProfileService(dir.Path));

        var first = service.AddClip(CreateDummyAudioFile(dir.Path, "a.wav"));
        var second = service.AddClip(CreateDummyAudioFile(dir.Path, "b.wav"));

        Assert.Equal(0, first.SortOrder);
        Assert.Equal(1, second.SortOrder);
    }

    [Fact]
    public void RemoveClip_RemovesFromCurrentProfile()
    {
        using var dir = new TempDirectory();
        var service = new ClipLibraryService(new ProfileService(dir.Path));
        var clip = service.AddClip(CreateDummyAudioFile(dir.Path));

        service.RemoveClip(clip.Id);

        Assert.Empty(service.CurrentProfile.Clips);
    }

    [Theory]
    [InlineData(-0.5, 0.0)]
    [InlineData(1.5, 1.0)]
    [InlineData(0.4, 0.4)]
    public void UpdateVolume_ClampsToValidRange(double input, double expected)
    {
        using var dir = new TempDirectory();
        var service = new ClipLibraryService(new ProfileService(dir.Path));
        var clip = service.AddClip(CreateDummyAudioFile(dir.Path));

        service.UpdateVolume(clip.Id, input);

        Assert.Equal(expected, service.CurrentProfile.Clips[0].Volume);
    }

    [Fact]
    public void MoveClip_ReordersSortOrder()
    {
        using var dir = new TempDirectory();
        var service = new ClipLibraryService(new ProfileService(dir.Path));
        var first = service.AddClip(CreateDummyAudioFile(dir.Path, "a.wav"));
        var second = service.AddClip(CreateDummyAudioFile(dir.Path, "b.wav"));
        var third = service.AddClip(CreateDummyAudioFile(dir.Path, "c.wav"));

        service.MoveClip(third.Id, 0);

        var ordered = service.CurrentProfile.Clips.OrderBy(c => c.SortOrder).ToList();
        Assert.Equal(new[] { third.Id, first.Id, second.Id }, ordered.Select(c => c.Id));
    }

    [Fact]
    public void ClipsChanged_FiresOnAdd()
    {
        using var dir = new TempDirectory();
        var service = new ClipLibraryService(new ProfileService(dir.Path));
        var raised = false;
        service.ClipsChanged += (_, _) => raised = true;

        service.AddClip(CreateDummyAudioFile(dir.Path));

        Assert.True(raised);
    }

    [Fact]
    public void LoadProfile_SwitchesCurrentProfile()
    {
        using var dir = new TempDirectory();
        var service = new ClipLibraryService(new ProfileService(dir.Path));
        service.AddClip(CreateDummyAudioFile(dir.Path));

        service.LoadProfile("Streaming");

        Assert.Equal("Streaming", service.CurrentProfile.Name);
        Assert.Empty(service.CurrentProfile.Clips);
    }
}
