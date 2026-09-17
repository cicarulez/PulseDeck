using PulseDeck.Core;
using Xunit;

public class AnimationTests
{
    private static readonly DisplaySnapshot Connected = new(true, "COM5", "tested", "connected");

    [Fact]
    public void TransportErrorSuspendsAnimationUntilExplicitToggleEvenAfterRecovery()
    {
        var guard = new AnimationGuard();
        Assert.Equal("enabled", guard.Select(true, "a.gif", Connected));
        Assert.Equal("suspended", guard.Select(true, "a.gif", Connected with { Status = "recovering", LastTransportError = "timeout" }));
        Assert.Equal("suspended", guard.Select(true, "a.gif", Connected));
        Assert.Equal("off", guard.Select(false, "a.gif", Connected));
        Assert.Equal("enabled", guard.Select(true, "a.gif", Connected));
    }

    [Fact]
    public void ManualDisconnectDoesNotSuspendPreviewAndOldErrorsDoNotDisableNewAttempt()
    {
        var guard = new AnimationGuard();
        Assert.Equal("enabled", guard.Select(true, "a.gif", Connected with { Connected = false, Status = "disconnected", UserDisconnected = true }));
        Assert.Equal("enabled", guard.Select(true, "a.gif", Connected with { LastTransportError = "old timeout" }));
        Assert.Equal("suspended", guard.Select(true, "a.gif", Connected with { Status = "error", LastTransportError = "new timeout" }));
        Assert.Equal("enabled", guard.Select(true, "b.gif", Connected));
    }
}
