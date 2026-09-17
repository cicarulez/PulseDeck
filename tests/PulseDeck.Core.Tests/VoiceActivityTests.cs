using PulseDeck.Core;
using Xunit;

public class VoiceActivityTests
{
    [Fact]
    public void ShortSpeechSurvivesOneDisplayTickAndThenExpires()
    {
        var activity = new VoiceActivity();
        var now = DateTimeOffset.UnixEpoch;
        activity.Update(1, false, now);
        Assert.False(activity.IsSpeaking(1, now)); // Joining silently is not speech.
        activity.Update(1, true, now.AddMilliseconds(100));
        activity.Update(1, false, now.AddMilliseconds(300));
        Assert.True(activity.IsSpeaking(1, now.AddSeconds(1)));
        activity.Update(1, false, now.AddMilliseconds(900)); // Duplicate stop cannot extend the highlight.
        Assert.False(activity.IsSpeaking(1, now.AddMilliseconds(1400)));
    }

    [Fact]
    public void LeavingOrDisconnectingClearsSpeakingImmediately()
    {
        var activity = new VoiceActivity();
        var now = DateTimeOffset.UnixEpoch;
        activity.Update(1, true, now);
        activity.Update(2, true, now);
        activity.Remove(1);
        Assert.False(activity.IsSpeaking(1, now));
        Assert.True(activity.IsSpeaking(2, now));
        activity.Clear();
        Assert.False(activity.IsSpeaking(2, now));
    }
}
