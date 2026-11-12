using FluentAssertions;
using Winhance.Core.Features.WimUtil.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class MediaLanguagesTests
{
    [Fact]
    public void Parse_MicrosoftIso_ReadsTheLanguage()
    {
        // Byte for byte what Win11_25H2_English_x64_v2 carries.
        var languages = MediaLanguages.Parse("\r\n[Available UI Languages]\r\nen-us = 3\r\n\r\n[Fallback Languages]\r\nen-us = en-us\r\n");

        languages.Select(c => c.Name).Should().Equal("en-US");
    }

    [Fact]
    public void Parse_MultiLanguageMedia_ReadsEveryLanguage()
    {
        var languages = MediaLanguages.Parse("[Available UI Languages]\nen-us = 3\nde-de = 3\n");

        languages.Select(c => c.Name).Should().Equal("en-US", "de-DE");
    }

    [Fact]
    public void Parse_NonLanguageNamesAndOtherSections_AreSkipped()
    {
        var languages = MediaLanguages.Parse("[Fallback Languages]\nfr-fr = en-us\n[Available UI Languages]\nnot-a-language = 3\n");

        languages.Should().BeEmpty();
    }
}
