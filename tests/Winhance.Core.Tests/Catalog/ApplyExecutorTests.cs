using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class ApplyExecutorTests
{
    private sealed class RecordingWriter : IStateWriter
    {
        public readonly List<string> Calls = new();
        public bool FailRegistryWrites;
        public bool WriteRegistry(RegTarget t, string path, object value) { Calls.Add($"W {path}={value}"); return !FailRegistryWrites; }
        public bool DeleteRegistry(RegTarget t, string path) { Calls.Add($"D {path}"); return true; }
        public bool EnsureRegistryKey(RegTarget t, string path) { Calls.Add($"E {path}"); return true; }
        public bool SetRegistryBit(RegTarget t, string path, int byteIndex, byte bitMask, bool set) { Calls.Add($"B {path}[{byteIndex}] 0x{bitMask:X2}={set}"); return !FailRegistryWrites; }
        public bool SetRegistryByte(RegTarget t, string path, int byteIndex, byte value) { Calls.Add($"Y {path}[{byteIndex}]=0x{value:X2}"); return !FailRegistryWrites; }
        public bool SetTask(TaskTarget t, bool enabled) { Calls.Add($"T {enabled}"); return true; }
        public bool RunEffect(Effect e) { Calls.Add("FX"); return true; }
    }

    private static RegTarget Reg() => new("K", new[] { @"HKLM\A" }, "V", RegistryValueKind.DWord);

    [Fact]
    public void Executes_every_op_and_reports_all_succeeded()
    {
        var w = new RecordingWriter();
        var plan = new ApplyOp[]
        {
            new RegistryWriteOp(Reg(), @"HKLM\A", 1),
            new EffectOp(new ScriptEffect("x.ps1", RunContext.System)),
        };
        var result = ApplyExecutor.Execute(plan, w);
        Assert.Equal(2, result.Total);
        Assert.True(result.AllSucceeded);
        Assert.Equal(new[] { @"W HKLM\A=1", "FX" }, w.Calls);
    }

    [Fact]
    public void Dispatches_bit_and_byte_ops_to_the_writer()
    {
        var w = new RecordingWriter();
        var plan = new ApplyOp[]
        {
            new RegistryBitSetOp(Reg(), @"HKLM\A", 4, 0x20, true),
            new RegistryByteSetOp(Reg(), @"HKLM\A", 8, 0x03),
        };
        var result = ApplyExecutor.Execute(plan, w);
        Assert.True(result.AllSucceeded);
        Assert.Equal(new[] { @"B HKLM\A[4] 0x20=True", @"Y HKLM\A[8]=0x03" }, w.Calls);
    }

    [Fact]
    public void Best_effort_continues_past_a_failure_and_records_it()
    {
        var w = new RecordingWriter { FailRegistryWrites = true };
        var plan = new ApplyOp[]
        {
            new RegistryWriteOp(Reg(), @"HKLM\A", 1),
            new EffectOp(new ScriptEffect("x.ps1", RunContext.System)),
        };
        var result = ApplyExecutor.Execute(plan, w);
        Assert.Equal(1, result.Failed);
        Assert.False(result.AllSucceeded);
        Assert.Contains("FX", w.Calls);
    }
}
