using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class ApplyExecutorTests
{
    private static readonly string[] HklmA = [@"HKLM\A"];
    private static readonly string[] WriteThenEffectCalls = [@"W HKLM\A=1", "FX"];
    private static readonly string[] BitThenByteCalls = [@"B HKLM\A[4] 0x20=True", @"Y HKLM\A[8]=0x03"];
    private static readonly string[] PowerPlanActivateCall = ["PP 11111111-1111-1111-1111-111111111111"];
    private static readonly string[] UnlockWriteLockCalls = [@"UNLK HKLM\A", @"W HKLM\A=4", @"LK HKLM\A"];
    private static readonly string[] CompositeSetThenRemoveCalls = [@"C HKCU\Pref[AutoHDREnable]=1", @"C HKCU\Pref[VRROptimizeEnable]=<del>"];
    private static readonly string[] PerSubkeyWriteThenDeleteCalls = [@"PW HKLM\Interfaces=1", @"PD HKLM\Interfaces"];
    private static readonly string[] WriteOnlyCall = [@"W HKLM\A=1"];
    private static readonly string[] EffectOnlyCall = ["FX"];

    private sealed class RecordingWriter : IStateWriter
    {
        public readonly List<string> Calls = new();
        public bool FailRegistryWrites;
        public bool WriteRegistry(RegTarget t, string path, object value) { Calls.Add($"W {path}={value}"); return !FailRegistryWrites; }
        public bool DeleteRegistry(RegTarget t, string path) { Calls.Add($"D {path}"); return true; }
        public bool EnsureRegistryKey(RegTarget t, string path) { Calls.Add($"E {path}"); return true; }
        public bool UnlockKey(RegTarget t, string path) { Calls.Add($"UNLK {path}"); return true; }
        public bool LockKey(RegTarget t, string path) { Calls.Add($"LK {path}"); return true; }
        public bool SetRegistryBit(RegTarget t, string path, int byteIndex, byte bitMask, bool set) { Calls.Add($"B {path}[{byteIndex}] 0x{bitMask:X2}={set}"); return !FailRegistryWrites; }
        public bool SetRegistryByte(RegTarget t, string path, int byteIndex, byte value) { Calls.Add($"Y {path}[{byteIndex}]=0x{value:X2}"); return !FailRegistryWrites; }
        public bool SetRegistryStringFlag(RegTarget t, string path, int flagMask, int absentBase, bool set) { Calls.Add($"F {path} 0x{flagMask:X2}={set}"); return !FailRegistryWrites; }
        public bool SetRegistryComposite(RegTarget t, string path, string compositeKey, string? subValue) { Calls.Add($"C {path}[{compositeKey}]={subValue ?? "<del>"}"); return !FailRegistryWrites; }
        public bool WriteRegistryPerSubkey(RegTarget t, string parentPath, object value) { Calls.Add($"PW {parentPath}={value}"); return !FailRegistryWrites; }
        public bool DeleteRegistryPerSubkey(RegTarget t, string parentPath) { Calls.Add($"PD {parentPath}"); return !FailRegistryWrites; }
        public bool SetTask(TaskTarget t, bool enabled) { Calls.Add($"T {enabled}"); return true; }
        public bool WritePowerCfgValue(PowerCfgTarget t, PowerContext context, int value) { Calls.Add($"P {context}={value}"); return !FailRegistryWrites; }
        public bool ActivatePowerPlan(string guid) { Calls.Add($"PP {guid}"); return true; }
        public bool RunEffect(Effect e) { Calls.Add("FX"); return true; }
    }

    private static RegTarget Reg() => new("K", HklmA, "V", RegistryValueKind.DWord);

    // Production always partitions before executing, so the tests go the same way.
    private static ApplyResult Execute(IReadOnlyList<ApplyOp> ops, IStateWriter writer) =>
        ApplyExecutor.Execute(ApplyPlan.From(ops), writer);

    [Fact]
    public void Executes_every_op_and_reports_all_succeeded()
    {
        var w = new RecordingWriter();
        var plan = new ApplyOp[]
        {
            new RegistryWriteOp(Reg(), @"HKLM\A", 1),
            new EffectOp(new NativePowerEffect(1, 0)),
        };
        var result = Execute(plan, w);
        Assert.Equal(2, result.Total);
        Assert.True(result.AllSucceeded);
        Assert.Equal(WriteThenEffectCalls, w.Calls);
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
        var result = Execute(plan, w);
        Assert.True(result.AllSucceeded);
        Assert.Equal(BitThenByteCalls, w.Calls);
    }

    [Fact]
    public void Dispatches_power_plan_activate_op_to_the_writer()
    {
        var w = new RecordingWriter();
        var plan = new ApplyOp[] { new PowerPlanActivateOp("11111111-1111-1111-1111-111111111111") };
        var result = Execute(plan, w);
        Assert.True(result.AllSucceeded);
        Assert.Equal(PowerPlanActivateCall, w.Calls);
    }

    [Fact]
    public void Dispatches_unlock_and_lock_ops_to_the_writer()
    {
        var w = new RecordingWriter();
        var plan = new ApplyOp[]
        {
            new RegistryUnlockKeyOp(Reg(), @"HKLM\A"),
            new RegistryWriteOp(Reg(), @"HKLM\A", 4),
            new RegistryLockKeyOp(Reg(), @"HKLM\A"),
        };
        var result = Execute(plan, w);
        Assert.True(result.AllSucceeded);
        Assert.Equal(UnlockWriteLockCalls, w.Calls);
    }

    [Fact]
    public void Dispatches_composite_set_and_remove_to_the_writer()
    {
        var w = new RecordingWriter();
        var plan = new ApplyOp[]
        {
            new RegistryCompositeSetOp(Reg(), @"HKCU\Pref", "AutoHDREnable", "1"),
            new RegistryCompositeSetOp(Reg(), @"HKCU\Pref", "VRROptimizeEnable", null),
        };
        var result = Execute(plan, w);
        Assert.True(result.AllSucceeded);
        Assert.Equal(CompositeSetThenRemoveCalls, w.Calls);
    }

    [Fact]
    public void Dispatches_per_subkey_ops_to_the_writer()
    {
        var w = new RecordingWriter();
        var plan = new ApplyOp[]
        {
            new RegistryPerSubkeyWriteOp(Reg(), @"HKLM\Interfaces", 1),
            new RegistryPerSubkeyDeleteOp(Reg(), @"HKLM\Interfaces"),
        };
        var result = Execute(plan, w);
        Assert.True(result.AllSucceeded);
        Assert.Equal(PerSubkeyWriteThenDeleteCalls, w.Calls);
    }

    [Fact]
    public void Best_effort_continues_past_a_failure_and_records_it()
    {
        var w = new RecordingWriter { FailRegistryWrites = true };
        var plan = new ApplyOp[]
        {
            new RegistryWriteOp(Reg(), @"HKLM\A", 1),
            new EffectOp(new NativePowerEffect(1, 0)),
        };
        var result = Execute(plan, w);
        Assert.Equal(1, result.Failed);
        Assert.False(result.AllSucceeded);
        Assert.Contains("FX", w.Calls);
    }

    [Fact]
    public void Process_launching_effects_never_reach_the_writer()
    {
        var w = new RecordingWriter();
        var script = new ScriptEffect("x.ps1", RunContext.System);
        var reg = new RegContentEffect("REGCONTENT");
        var plan = ApplyPlan.From(new ApplyOp[]
        {
            new RegistryWriteOp(Reg(), @"HKLM\A", 1),
            new EffectOp(script),
            new EffectOp(reg),
        });

        var outcome = ApplyExecutor.Execute(plan, w);

        Assert.Equal(WriteOnlyCall, w.Calls);
        Assert.Equal(new Effect[] { script, reg }, plan.AsyncEffects);
        Assert.True(outcome.AllSucceeded);
        // Total counts what the executor RAN; the plan counts both halves.
        Assert.Equal(1, outcome.Total);
        Assert.Equal(3, plan.Total);
    }

    [Fact]
    public void Blocking_effects_stay_on_the_writer()
    {
        var w = new RecordingWriter();
        var plan = ApplyPlan.From(new ApplyOp[] { new EffectOp(new NativePowerEffect(1, 0)) });

        var outcome = ApplyExecutor.Execute(plan, w);

        Assert.Equal(EffectOnlyCall, w.Calls);
        Assert.Empty(plan.AsyncEffects);
        Assert.True(outcome.AllSucceeded);
    }

    [Fact]
    public void A_plan_with_no_async_effects_keeps_its_op_list_intact()
    {
        var ops = new ApplyOp[] { new RegistryWriteOp(Reg(), @"HKLM\A", 1) };

        var plan = ApplyPlan.From(ops);

        Assert.Empty(plan.AsyncEffects);
        Assert.Same(ops, plan.SyncOps);
    }

    [Fact]
    public void Partitioning_preserves_the_authored_order_within_each_half()
    {
        var first = new ScriptEffect("first", RunContext.System);
        var second = new RegContentEffect("second");
        var plan = ApplyPlan.From(new ApplyOp[]
        {
            new EffectOp(first),
            new RegistryWriteOp(Reg(), @"HKLM\A", 1),
            new EffectOp(new NativePowerEffect(1, 0)),
            new EffectOp(second),
            new RegistryDeleteOp(Reg(), @"HKLM\B"),
        });

        Assert.Equal(new Effect[] { first, second }, plan.AsyncEffects);
        Assert.Equal(3, plan.SyncOps.Count);
    }
}
