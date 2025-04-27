using Microsoft.Win32;
using System.Collections.Generic;

namespace Winhance.Core.Features.Optimize.Models;

public static class UacOptimizations
{
    public const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
    public const string RegistryName = "ConsentPromptBehaviorAdmin";
    public static readonly RegistryValueKind ValueKind = RegistryValueKind.DWord;

    public static readonly Dictionary<int, int> LevelToRegistryValue = new()
    {
        { 0, 0 }, // Never notify (Low)
        { 1, 5 }, // Notify me only (Moderate)
        { 2, 2 }  // Always notify (High)
    };

    public static readonly Dictionary<int, int> RegistryValueToLevel = new()
    {
        { 0, 0 }, // Never notify
        { 5, 1 }, // Notify me only
        { 2, 2 }  // Always notify
    };
}


