using PulseDeck.Core;
using Xunit;

public class TurzxVideoOverlayProtocolTests
{
    [Fact]
    public void ExperimentalCommandsKeepProductionFullCommandUnchanged()
    {
        Assert.Equal("CAEF690038400E10", Convert.ToHexString(TurzxVideoOverlayProtocol.InitialiseCommand()[..8]));
        Assert.Equal("D0EF6900000002", Convert.ToHexString(TurzxVideoOverlayProtocol.EmptyVisibilityCommand()[..7]));
        Assert.Equal("C8EF690038400E10", Convert.ToHexString(TurzxProtocol.FullFrameCommand()[..8]));
    }

    [Fact]
    public void SmallOpaqueOverlayIncludesCounterVisibilityAndPortraitAddresses()
    {
        var pixels = new byte[1920 * 480 * 4];
        var rect = new FrameRect(2, 3, 2, 2);
        for (int y = 3; y < 5; y++) for (int x = 2; x < 4; x++) pixels[(y * 1920 + x) * 4 + 3] = 255;
        var (header, payload) = TurzxVideoOverlayProtocol.Frame(pixels, rect, 47);
        Assert.Equal("CCEF69000000260000000000002F0000000A", Convert.ToHexString(header[..18]));
        Assert.Equal("00059B0002", Convert.ToHexString(payload[..5]));
        Assert.Equal("00059B000200077B0002EF69", Convert.ToHexString(payload[26..38]));
        Assert.Equal(250, header.Length);
        Assert.Equal(250, payload.Length);
        Assert.All(payload[38..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void InvalidAndTransparentRegionsCannotBeSentAsOpaqueOverlays()
    {
        var pixels = new byte[1920 * 480 * 4];
        Assert.Throws<ArgumentException>(() => TurzxVideoOverlayProtocol.Frame(pixels, new(0, 0, 1, 1), 0));
        foreach (var rect in new[] { new FrameRect(-1, 0, 1, 1), new(0, 0, 0, 1), new(1919, 0, 2, 1), new(0, 0, 1920, 480) })
            Assert.Throws<ArgumentOutOfRangeException>(() => TurzxVideoOverlayProtocol.Frame(pixels, rect, 0));
        Assert.Throws<ArgumentException>(() => TurzxVideoOverlayProtocol.Frame([], new(0, 0, 1, 1), 0));
    }
}
