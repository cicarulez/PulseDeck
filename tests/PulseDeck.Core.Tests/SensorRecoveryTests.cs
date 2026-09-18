using PulseDeck.Core;
using Xunit;
public class SensorRecoveryTests
{
    [Fact]
    public void RetriesOnlyAfterSustainedLossAndStopsUntilStableRecovery()
    {
        var p=new SensorRecovery();var now=DateTimeOffset.UtcNow;
        Assert.False(p.Observe(true,false,true,now));
        Assert.False(p.Observe(true,false,true,now.AddSeconds(29)));
        Assert.True(p.Observe(true,false,true,now.AddSeconds(30)));
        Assert.False(p.Observe(true,false,true,now.AddSeconds(89)));
        Assert.True(p.Observe(true,false,true,now.AddSeconds(90)));
        Assert.True(p.Observe(true,false,true,now.AddSeconds(210)));
        Assert.False(p.Observe(true,false,true,now.AddHours(1)));
        p.Observe(true,true,true,now.AddHours(2));
        p.Observe(true,true,true,now.AddHours(2).AddSeconds(60));
        Assert.Equal(0,p.Attempts);
        p.Observe(true,false,true,now.AddHours(3));
        Assert.True(p.Observe(true,false,true,now.AddHours(3).AddSeconds(30)));
    }
    [Fact]
    public void MissingPermissionsOrUnconfiguredSensorNeverTriggersRecovery()
    {
        var p=new SensorRecovery();var now=DateTimeOffset.UtcNow;
        p.Observe(true,false,false,now);
        Assert.False(p.Observe(true,false,false,now.AddHours(1)));
        Assert.False(p.Observe(false,false,true,now.AddHours(2)));
        Assert.False(p.Observe(true,true,true,now.AddHours(3))); // Healthy includes zero RPM.
        Assert.Equal(0,p.Attempts);
    }
}
