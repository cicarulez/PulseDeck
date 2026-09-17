using PulseDeck.Core;
using Xunit;

public class ProtocolTests
{
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
