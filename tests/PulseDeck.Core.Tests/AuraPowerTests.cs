using PulseDeck.Core;
using Xunit;

public class AuraPowerTests
{
    [Fact]
    public void OffAtBootDoesNotRequireAnEarlierColorButUnavailableAuraNeverDims()
    {
        Assert.Equal("off", AuraPower.Apply(new("waiting"), true).Status);
        Assert.Equal("off", AuraPower.Apply(new("unsupported"), true).Status);
        Assert.Null(AuraPower.Apply(new("connected", "#FFFF00", Colors: ["#FFFF00"]), true).Colors);
        foreach (var status in new[] { "unavailable", "incompatible", "disabled" })
        {
            var snapshot = AuraPower.Apply(new(status), true);
            Assert.Equal(status, snapshot.Status);
            Assert.Null(snapshot.LightingOff);
        }
        Assert.Equal("connected", AuraPower.Apply(new("connected"), null).Status);
        Assert.Equal("waiting", AuraPower.Apply(new("waiting"), false).Status);
    }
    private static string Profile(string enabled, bool external = true) =>
        $"<root><header>ASUS_AURA</header>{(external ? "<ingroupdevice key=\"EXTERNAL_GENERAL\">29</ingroupdevice>" : "")}"
        + $"<device key=\"Group\"><isenabled>{enabled}</isenabled><scene key=\"Non-S0\"><isenabled>0</isenabled></scene></device></root>";

    [Fact]
    public void ObservedGroupSwitchDistinguishesOffAndStaticWithoutUsingColorOrSleepScene()
    {
        Assert.True(AuraPower.ReadOff(Profile("0")));
        Assert.False(AuraPower.ReadOff(Profile("1")));
        Assert.Null(AuraPower.ReadOff(Profile("0", external: false)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<root>")]
    [InlineData("<root><header>OTHER</header></root>")]
    [InlineData("<!DOCTYPE root [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><root>&x;</root>")]
    public void MissingMalformedAndExternalEntityProfilesNeverTurnOffThePanel(string xml) =>
        Assert.Null(AuraPower.ReadOff(xml));

    [Fact]
    public void UnknownAndAmbiguousSwitchesNeverTurnOffThePanel()
    {
        Assert.Null(AuraPower.ReadOff(Profile("2")));
        Assert.Null(AuraPower.ReadOff(Profile("")));
        Assert.Null(AuraPower.ReadOff(Profile("0").Replace("</root>", "<device key=\"Group\"><isenabled>0</isenabled></device></root>")));
        Assert.Null(AuraPower.ReadOff(Profile("0").Replace("</isenabled>", "</isenabled><isenabled>0</isenabled>")));
        Assert.Null(AuraPower.ReadOff(Profile("0").Replace(">29<", ">invalid<")));
        Assert.Null(AuraPower.ReadOff(Profile("0").Replace("</root>", new string(' ', 262144) + "</root>")));
    }

    [Fact]
    public void OnlyVerifiedOffWithAuraAndKnownRestoreBrightnessCanOverrideTheSavedValue()
    {
        var config = new DeckConfig { AuraEnabled = true, DisplayBrightness = 25 };
        var off = new AuraSnapshot("off", LightingOff: true);
        Assert.Equal(0, AuraPower.Brightness(config, off));
        Assert.Equal(25, config.DisplayBrightness);
        Assert.Equal(25, AuraPower.Brightness(config, new("connected", "#000000", LightingOff: false)));
        Assert.Equal(25, AuraPower.Brightness(config, new("unavailable", LightingOff: true)));
        Assert.Equal(25, AuraPower.Brightness(config, new("off")));
        Assert.Equal(25, AuraPower.Brightness(config with { AuraEnabled = false }, off));
        Assert.Null(AuraPower.Brightness(config with { DisplayBrightness = null }, off));
        Assert.Equal(70, AuraPower.Brightness(config with { DisplayBrightness = 70 }, new("connected")));
    }

    [Fact]
    public void GamingRestoresBrightnessAndLeavingGamingFollowsDarkAuraAgain()
    {
        var controller = new AuraBrightness();
        var config = new DeckConfig { AuraEnabled = true, DisplayBrightness = 25 };
        var off = new AuraSnapshot("off", LightingOff: true);
        Assert.Equal(0, controller.Resolve(config, off, "desktop"));
        Assert.True(controller.Off);
        Assert.Equal(25, controller.Resolve(config, off, "gaming"));
        Assert.False(controller.Off);
        Assert.Equal(70, controller.Resolve(config with { DisplayBrightness = 70 }, off, "gaming"));
        Assert.Equal(0, controller.Resolve(config, off, "music"));
        Assert.True(controller.Off);
        Assert.Equal(25, controller.Resolve(config with { DisplayBrightness = null }, off, "gaming"));
        Assert.False(controller.Off);
        Assert.Null(controller.Resolve(config with { DisplayBrightness = null }, off, "gaming"));
    }

    [Fact]
    public void ForcedGamingIgnoresDarkAuraFromStartupAndRespectsManualBrightness()
    {
        var config = new DeckConfig { AuraEnabled = true, DisplayBrightness = 25, ProfileMode = "gaming" };
        var off = new AuraSnapshot("off", LightingOff: true);
        Assert.Equal(25, AuraPower.Brightness(config, off));
        Assert.Equal(0, AuraPower.Brightness(config with { DisplayBrightness = 0 }, off));
        Assert.Null(AuraPower.Brightness(config with { DisplayBrightness = null }, off));
        Assert.Equal(0, AuraPower.Brightness(config with { ProfileMode = "desktop" }, off, "gaming"));
        var controller = new AuraBrightness();
        Assert.Equal(25, controller.Resolve(config, off));
        Assert.False(controller.Off);
    }

    [Fact]
    public void OffRoundTripAndConfigChangesRestoreTheCorrectValue()
    {
        var controller = new AuraBrightness();
        var config = new DeckConfig { AuraEnabled = true, DisplayBrightness = 25 };
        var off = new AuraSnapshot("off", LightingOff: true);
        Assert.Equal(25, controller.Resolve(config, new("connected")));
        Assert.Equal(0, controller.Resolve(config, off));
        Assert.True(controller.Off);
        Assert.Equal(0, controller.Resolve(config with { DisplayBrightness = 40 }, off));
        Assert.Equal(40, controller.Resolve(config with { DisplayBrightness = 40 }, new("connected")));
        Assert.False(controller.Off);
        Assert.Equal(0, controller.Resolve(config, off));
        Assert.Equal(25, controller.Resolve(config with { DisplayBrightness = null }, off));
        Assert.Null(controller.Resolve(config with { DisplayBrightness = null }, off));
        Assert.Equal(0, controller.Resolve(config, off));
        Assert.Equal(25, controller.Resolve(config, new("unavailable")));
        Assert.False(controller.Off);
        Assert.Equal(0, controller.Resolve(config, off));
        Assert.Equal(25, controller.Resolve(config with { AuraEnabled = false }, off));
        Assert.Equal(0, controller.Resolve(config with { DisplayBrightness = 70 }, off));
        Assert.Equal(70, controller.Resolve(config with { DisplayBrightness = null }, off));
    }
}
