using PulseDeck.Core;
using Xunit;

public class ProtocolTests
{
    [Fact]
    public void DistantChangedBandsAvoidSendingTheUnchangedMiddleAndCoverEveryPixel()
    {
        var before = Frame(); var after = Frame();
        for (int y = 20; y < 40; y++) for (int x = 30; x < 180; x++) after[(y * 1920 + x) * 4] = 1;
        for (int y = 400; y < 420; y++) for (int x = 1400; x < 1800; x++) after[(y * 1920 + x) * 4] = 2;
        var regions = TurzxProtocol.ChangedRegions(before, after);
        Assert.Equal(2, regions.Length);
        Assert.True(regions.Sum(r => r.Width * r.Height) < 20000);
        for (int y = 0; y < 480; y++) for (int x = 0; x < 1920; x++)
            if (after[(y * 1920 + x) * 4] != 0) Assert.Contains(regions, r => x >= r.X && x < r.X + r.Width && y >= r.Y && y < r.Y + r.Height);
        Assert.Empty(TurzxProtocol.ChangedRegions(before, before));
    }
    [Fact]
    public void BatchedPayloadAddressesSeparateRegionsWithoutChangingPixelsBetweenThem()
    {
        var pixels = Frame();
        FrameRect[] regions = [new(10, 20, 15, 4), new(10, 400, 15, 5), new(1700, 200, 2, 3)];
        foreach (var rect in regions)
            for (int y = rect.Y; y < rect.Y + rect.Height; y++) for (int x = rect.X; x < rect.X + rect.Width; x++)
                for (int c = 0; c < 4; c++) pixels[(y * 1920 + x) * 4 + c] = (byte)(x + y + c);
        var (header, encoded) = TurzxProtocol.PartialFrameRegions(pixels, regions, 90, 42);
        int rawLength = (header[4] << 16 | header[5] << 8 | header[6]) - 2;
        var raw = new byte[rawLength];
        for (int i = 0, source = 0; i < rawLength; i++)
        {
            if (i > 0 && i % 249 == 0) { Assert.Equal(0, encoded[source]); source++; }
            raw[i] = encoded[source++];
        }
        var reconstructed = Frame(); var lastAddress = -1;
        for (int pos = 0; pos < raw.Length;)
        {
            int address = raw[pos++] << 16 | raw[pos++] << 8 | raw[pos++];
            int length = raw[pos++] << 8 | raw[pos++];
            Assert.True(address > lastAddress); lastAddress = address;
            for (int i = 0; i < length; i++)
            {
                int x = (address + i) / 480, y = 479 - (address + i) % 480;
                raw.AsSpan(pos, 4).CopyTo(reconstructed.AsSpan((y * 1920 + x) * 4, 4)); pos += 4;
            }
        }
        Assert.Equal(pixels, reconstructed);
        Assert.Equal(new byte[] { 0, 0, 0, 42 }, header[10..14]);
        Assert.Throws<ArgumentOutOfRangeException>(() => TurzxProtocol.PartialFrameRegions(pixels, [], 90, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TurzxProtocol.PartialFrameRegions(pixels, [new(0, 0, 1921, 1)], 90, 0));
    }
    private static byte[] Frame() => new byte[1920 * 480 * 4];
    [Theory]
    [InlineData(@"USB\VID_1A86&PID_CA88\CT88INCH", true)]
    [InlineData(@"usb\vid_1a86&pid_ca88\ct88inch", true)]
    [InlineData(@"USB\VID_1A86&PID_CA88\OTHER", false)]
    [InlineData(@"USB\VID_1A86&PID_CA21\CT21INCH", false)]
    [InlineData("COM3", false)]
    public void StandbyWakeRequiresTheObservedEightInchIdentity(string identity, bool supported)
        => Assert.Equal(supported, TurzxProtocol.IsSupportedStandbyDevice(identity));
    [Theory]
    [InlineData("video", "79EF6900000001")]
    [InlineData("media", "96EF6900000001")]
    [InlineData("off", "83EF6900000001")]
    public void ShutdownCommandsMatchUpstreamScreenOff(string operation, string prefix)
    {
        var command = operation switch
        {
            "video" => TurzxProtocol.StopVideoCommand(),
            "media" => TurzxProtocol.StopMediaCommand(),
            _ => TurzxProtocol.ScreenOffCommand()
        };
        Assert.Equal(Convert.FromHexString(prefix), command[..7]);
        Assert.Equal(250, command.Length);
        Assert.All(command[7..], b => Assert.Equal(0, b));
    }
    [Fact]
    public void FullFrameCommandMatchesSuccessfulPythonProbe()
    {
        var command = TurzxProtocol.FullFrameCommand();
        Assert.Equal(new byte[] { 0xc8, 0xef, 0x69, 0x00, 0x38, 0x40, 0x0e, 0x10 }, command[..8]);
        Assert.Equal(250, command.Length);
        Assert.All(command[8..], b => Assert.Equal(0, b));
    }
    [Fact]
    public void SinglePixelChangeHasExactBounds()
    {
        var before = Frame(); var after = Frame(); after[(311 * 1920 + 23) * 4] = 255;
        Assert.Null(TurzxProtocol.ChangedRegion(before, before));
        Assert.Equal(new FrameRect(23, 311, 1, 1), TurzxProtocol.ChangedRegion(before, after));
    }
    [Fact]
    public void PartialPacketUsesRotatedAddressAndRom90Bgra()
    {
        var frame = Frame(); frame[0] = 11; frame[1] = 22; frame[2] = 33; frame[3] = 255;
        var (header, payload) = TurzxProtocol.PartialFrame(frame, new(0, 0, 1, 1), 90, 42);
        Assert.Equal(new byte[] { 0xcc, 0xef, 0x69, 0, 0, 0, 11, 0, 0, 0, 0, 0, 0, 42 }, header[..14]);
        Assert.Equal(new byte[] { 0, 1, 223, 0, 1, 11, 22, 33, 255, 0xef, 0x69 }, payload[..11]);
        Assert.Equal(250, payload.Length);
    }
    [Fact]
    public void FullFrameRotatesBottomLeftToFirstDevicePixelAndStuffsBlocks()
    {
        var frame = Frame(); int index = (479 * 1920) * 4;
        frame[index] = 9; frame[index + 3] = 255;
        var encoded = TurzxProtocol.FullFrame(frame);
        Assert.Equal(9, encoded[0]); Assert.Equal(255, encoded[3]); Assert.Equal(0, encoded[249]);
        Assert.Equal(0, encoded.Length % 250);
    }
    [Fact]
    public void RejectsOutOfBoundsBeforeSending()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TurzxProtocol.PartialFrame(Frame(), new(1919, 479, 2, 1), 90, 0));
        Assert.Throws<ArgumentException>(() => TurzxProtocol.FullFrame(new byte[5]));
    }
}
