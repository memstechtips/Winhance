namespace Winhance.UI.Features.Common.Interfaces;

// A page that fills itself in after it loads; the frame keeps its loader up until this completes.
public interface IPageReadiness
{
    Task WhenReadyAsync();
}
