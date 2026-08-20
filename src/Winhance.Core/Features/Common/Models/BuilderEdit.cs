using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models;

// Numeric values are stored in SYSTEM units - what the exporter writes and the config format has always held;
// the ViewModel converts on the way in.
public class BuilderEdit
{
    public string SettingId { get; set; } = string.Empty;
    public InputType InputType { get; set; }

    public bool? IsSelected { get; set; }

    public int? SelectedIndex { get; set; }

    public Dictionary<string, object>? CustomStateValues { get; set; }

    public int? NumericValue { get; set; }

    public int? AcNumericValue { get; set; }

    public int? DcNumericValue { get; set; }

    public int? AcIndex { get; set; }

    public int? DcIndex { get; set; }
}
