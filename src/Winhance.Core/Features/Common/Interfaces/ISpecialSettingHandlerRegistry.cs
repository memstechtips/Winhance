// File: src/Winhance.Core/Features/Common/Interfaces/ISpecialSettingHandlerRegistry.cs
namespace Winhance.Core.Features.Common.Interfaces;

public interface ISpecialSettingHandlerRegistry
{
    ISpecialSettingHandler? TryGet(string settingId);
}
