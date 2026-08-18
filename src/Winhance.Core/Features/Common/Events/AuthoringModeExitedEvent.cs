namespace Winhance.Core.Features.Common.Events;

/// <summary>
/// Raised when the app leaves a mode that authors intent without applying it — Builder today, any
/// future mode that declares <see cref="Models.ModeCapabilities.AuthorsIntent"/> automatically.
///
/// Such a mode moves toggles and selections on the shared setting ViewModels without touching the
/// machine, so on exit those positions are fiction and the loaded settings must be reloaded from
/// live system state to be truthful again.
///
/// Named for the capability rather than for Builder on purpose: the publisher tests
/// <c>ModeCapabilities.For(previous).AuthorsIntent</c>, so a second authoring mode is covered by
/// declaring its capabilities and nothing else. An event called "BuilderModeExited" would invite
/// the next one to add a parallel event and a parallel handler instead.
/// </summary>
public class AuthoringModeExitedEvent : IDomainEvent
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public Guid EventId { get; } = Guid.NewGuid();
}
