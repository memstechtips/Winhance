using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Infrastructure.Features.Autounattend;
using Xunit;

namespace Winhance.Infrastructure.Tests.Autounattend;

// A setting two writers claim is written twice, and one the generic writer thinks is claimed but nobody writes
// drops out of the file. Neither shows until someone reads the XML.
public class WriterOwnershipTests
{
    private static readonly IReadOnlyList<IAutounattendElementWriter> Writers = AutounattendDocumentBuilder.ShapeWriters;

    private static List<string> Claims() => Writers.SelectMany(writer => writer.Handles).ToList();

    [Fact]
    public void No_two_writers_claim_the_same_setting()
    {
        Claims().Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Every_claimed_id_names_an_answer_file_setting()
    {
        foreach (var id in Claims())
        {
            var setting = SettingCatalog.Find(id);

            setting.Should().NotBeNull(id);
            setting!.IsAnswerFileOnly.Should().BeTrue(id);
        }
    }

    [Fact]
    public void The_generic_writer_skips_exactly_what_the_others_claim()
    {
        AutounattendDocumentBuilder.Elements.Claimed.Should().BeEquivalentTo(Claims());
    }

    [Fact]
    public void Every_unclaimed_answer_file_setting_has_something_a_writer_or_the_script_can_render()
    {
        var claimed = Claims();
        foreach (var setting in SettingCatalog.All.Where(s => s.IsAnswerFileOnly && !claimed.Contains(s.Id)))
        {
            var renderable = setting.Targets.OfType<AutounattendElement>().Any()
                || setting.States.SelectMany(s => s.Effects).OfType<AutounattendCommand>().Any()
                || setting.States.SelectMany(s => s.Effects).OfType<ScriptEffect>().Any();

            renderable.Should().BeTrue(setting.Id);
        }
    }
}
