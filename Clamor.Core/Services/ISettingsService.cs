using Clamor.Core.Models;

namespace Clamor.Core.Services;

public interface ISettingsService
{
    AppSettings Load();

    void Save(AppSettings settings);
}
