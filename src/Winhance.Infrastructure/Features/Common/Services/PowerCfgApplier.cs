using Windows.Win32;
using Windows.Win32.Foundation;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class PowerCfgApplier(
    IHardwareDetectionService hardwareDetectionService,
    ILogService logService) : IPowerCfgApplier
{
    // Serializes the write/commit pair and owns the batch depth. Contention is not the reason - a
    // commit re-activates the whole scheme, so "the last writer out commits" only means anything if
    // writers cannot interleave.
    private readonly object _writeLock = new();
    private int _batchDepth;
    private bool _commitPending;

    public IDisposable BeginBatch()
    {
        lock (_writeLock)
            _batchDepth++;
        return new BatchScope(this);
    }

    public bool WriteValueIndex(PowerCfgTarget target, PowerContext context, int value, Guid? scheme = null)
    {
        // The apply engine emits one PowerCfgSetOp per context, so the writer calls this once per context (AC then
        // DC). Writing the same value again is a no-op on disk, so it is omitted here.
        // Unknown battery state attempts the write: a wasted DC write on a desktop is harmless, a skipped
        // one on a laptop loses the setting. A scheme being authored carries both halves regardless of the
        // hardware stamping it - that plan may be exported and used on a laptop.
        bool hasBattery = scheme is not null || (hardwareDetectionService.HasBattery() ?? true);
        if (context == PowerContext.DC && !hasBattery)
        {
            logService.Log(LogLevel.Debug, $"Skipping DC write for {target.SettingGuid} - no battery present");
            return true;
        }

        var subgroupGuid = Guid.Parse(target.SubgroupGuid);
        var settingGuid = Guid.Parse(target.SettingGuid);

        lock (_writeLock)
        {
            Guid schemeGuid;
            if (scheme is { } named)
            {
                schemeGuid = named;
            }
            else if (!TryGetActiveScheme(out schemeGuid))
            {
                return false;
            }

            // The metadata types the DC variant's return as a plain uint and the AC variant's as WIN32_ERROR,
            // so both are normalised here rather than at every comparison below.
            uint rc = context == PowerContext.DC
                ? PInvoke.PowerWriteDCValueIndex(null, schemeGuid, subgroupGuid, settingGuid, (uint)value)
                : (uint)PInvoke.PowerWriteACValueIndex(null, schemeGuid, subgroupGuid, settingGuid, (uint)value);

            // "Changes to the settings for the active power scheme do not take effect until you call the
            // PowerSetActiveScheme function" - the requirement is scoped to the ACTIVE scheme, so a write
            // aimed at any other scheme needs no commit at all; it applies when that scheme is next
            // activated. Inside a batch the commit is deferred to the end, because one re-activation
            // commits every write that preceded it and each one costs ~80ms on real hardware.
            uint commitRc = (uint)WIN32_ERROR.ERROR_SUCCESS;
            string commitNote;
            if (scheme is not null)
            {
                commitNote = "not needed, inactive scheme";
            }
            else if (_batchDepth > 0)
            {
                _commitPending = true;
                commitNote = "deferred to end of batch";
            }
            else
            {
                commitRc = (uint)PInvoke.PowerSetActiveScheme(null, schemeGuid);
                commitNote = commitRc.ToString();
            }

            var applied = rc == (uint)WIN32_ERROR.ERROR_SUCCESS && commitRc == (uint)WIN32_ERROR.ERROR_SUCCESS;
            logService.Log(applied ? LogLevel.Info : LogLevel.Error,
                $"{(applied ? "Wrote" : "Failed to write")} {context} value index {value} for setting {target.SettingGuid} (rc={rc}, commit {commitNote})");
            return applied;
        }
    }

    private unsafe bool TryGetActiveScheme(out Guid schemeGuid)
    {
        schemeGuid = Guid.Empty;
        if (PInvoke.PowerGetActiveScheme(null, out Guid* active) != WIN32_ERROR.ERROR_SUCCESS || active is null)
        {
            logService.Log(LogLevel.Error, "Failed to get active power scheme");
            return false;
        }

        schemeGuid = *active;
        PInvoke.LocalFree((HLOCAL)(IntPtr)active);
        return true;
    }

    private void EndBatch()
    {
        lock (_writeLock)
        {
            if (--_batchDepth > 0 || !_commitPending)
                return;

            _commitPending = false;
            if (!TryGetActiveScheme(out var schemeGuid))
                return;

            var rc = PInvoke.PowerSetActiveScheme(null, schemeGuid);
            logService.Log(rc == WIN32_ERROR.ERROR_SUCCESS ? LogLevel.Info : LogLevel.Error,
                $"Committed the batch's writes with one scheme re-activation (rc={rc})");
        }
    }

    private sealed class BatchScope(PowerCfgApplier owner) : IDisposable
    {
        private bool _closed;

        public void Dispose()
        {
            if (_closed)
                return;
            _closed = true;
            owner.EndBatch();
        }
    }
}
