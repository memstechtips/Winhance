using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// A single setting change recorded during a Builder-mode session. Builder Save
/// merges these onto the system-seeded base configuration so the saved file reflects
/// the user's authored intent rather than only the live system state.
///
/// Every input type is captured. Numeric values are stored in SYSTEM units, matching what the
/// exporter writes into <c>ConfigurationItem.PowerSettings</c> and what the config format has always
/// held — the ViewModel converts on the way in, so no consumer has to know the display units.
/// </summary>
public class BuilderEdit
{
    public string SettingId { get; set; } = string.Empty;
    public InputType InputType { get; set; }

    /// <summary>For Toggle / CheckBox / Action: the recorded on/off (or "include") state.</summary>
    public bool? IsSelected { get; set; }

    /// <summary>For Selection on a predefined option: the chosen combo-box index.</summary>
    public int? SelectedIndex { get; set; }

    /// <summary>For Selection seeded at the Custom index: the raw values to write.</summary>
    public Dictionary<string, object>? CustomStateValues { get; set; }

    /// <summary>For a single-context NumericRange: the value, in SYSTEM units.</summary>
    public int? NumericValue { get; set; }

    /// <summary>For an AC/DC-separate NumericRange: the AC value, in SYSTEM units.</summary>
    public int? AcNumericValue { get; set; }

    /// <summary>For an AC/DC-separate NumericRange: the DC value, in SYSTEM units.</summary>
    public int? DcNumericValue { get; set; }

    /// <summary>For an AC/DC-separate Selection: the chosen AC option index.</summary>
    public int? AcIndex { get; set; }

    /// <summary>For an AC/DC-separate Selection: the chosen DC option index.</summary>
    public int? DcIndex { get; set; }
}
