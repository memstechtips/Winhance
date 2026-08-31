namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal readonly record struct DismImageEntry(int Index, string Name);

internal interface IDismImageInfoReader
{
    // DISM rather than WIMGAPI: WIMCreateFile documents three compression values (NONE, XPRESS,
    // LZX) and fails to open an install.esd at all, while DISM reads both WIM and ESD.
    IReadOnlyList<DismImageEntry> GetImageInfo(string imagePath);
}
