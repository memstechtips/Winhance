namespace Winhance.Core.Features.WimUtil.Models;

// An image's EditionId mapped to the autounattend-edition option carrying its generic install key. Windows 10 and
// Windows 11 share those keys, so one table covers media of either.
public static class MediaEditions
{
    private static readonly Dictionary<string, int> OptionByEditionId = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Core"] = 1,
        ["CoreN"] = 2,
        ["CoreSingleLanguage"] = 3,
        ["Professional"] = 4,
        ["ProfessionalN"] = 5,
        ["ProfessionalWorkstation"] = 6,
        ["ProfessionalWorkstationN"] = 7,
        ["ProfessionalEducation"] = 8,
        ["ProfessionalEducationN"] = 9,
        ["Education"] = 10,
        ["EducationN"] = 11,
        ["Enterprise"] = 12,
        ["EnterpriseN"] = 13,
    };

    public static int? OptionFor(string editionId) =>
        OptionByEditionId.TryGetValue(editionId, out var option) ? option : null;

    // False for the options that name no edition: ask during setup, a custom key, the firmware key.
    private static bool NamesAnEdition(int option) => OptionByEditionId.ContainsValue(option);

    // Setup installs the image's one edition whatever the answer file picks, so a pick naming another edition misleads.
    public static bool PicksAnother(int? option, string keptEditionId) =>
        option is { } pick && NamesAnEdition(pick) && OptionFor(keptEditionId) != pick;
}
