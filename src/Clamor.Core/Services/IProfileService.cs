using Clamor.Core.Models;

namespace Clamor.Core.Services;

public interface IProfileService
{
    string ProfilesDirectory { get; }

    IReadOnlyList<string> GetProfileNames();

    bool Exists(string name);

    Profile Load(string name);

    Profile LoadOrCreate(string name);

    void Save(Profile profile);

    void Delete(string name);

    void Rename(string oldName, string newName);
}
