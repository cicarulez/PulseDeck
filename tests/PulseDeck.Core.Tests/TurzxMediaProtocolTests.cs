using System.Text;
using PulseDeck.Core;
using Xunit;

public class TurzxMediaProtocolTests
{
    [Fact]
    public void UploadAndPlaybackPreserveBootMediaAndDeclareOriginalFileSize()
    {
        const string path = "/root/video/pulsedeck-test.mp4";
        var upload = TurzxMediaProtocol.UploadVideo(path, 26767);
        Assert.Equal(0x6f, upload[0]);
        Assert.Equal(new byte[] { 0x8f, 0x68, 0, 0 }, upload[(10 + path.Length)..(14 + path.Length)]);
        var start = TurzxMediaProtocol.StartVideo(path);
        Assert.Equal(0x78, start[0]); Assert.Equal(1, start[7]);
        Assert.Equal(0x66, TurzxMediaProtocol.DeleteVideo(path)[0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => TurzxMediaProtocol.UploadVideo(path, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TurzxMediaProtocol.UploadVideo(path, 131073));
    }
    [Theory]
    [InlineData("/root/video/")]
    [InlineData("/mnt/SDCARD/video/")]
    public void DirectoryRequestMatchesPublicVideoProtocol(string path)
    {
        var packet = TurzxMediaProtocol.ListVideos(path);
        Assert.Equal(250, packet.Length);
        Assert.Equal(new byte[] { 0x65, 0xef, 0x69, 0, 0, 0, (byte)path.Length, 0, 0, 0 }, packet[..10]);
        Assert.Equal(path, Encoding.ASCII.GetString(packet, 10, path.Length));
        Assert.All(packet[(10 + path.Length)..], b => Assert.Equal(0, b));
        Assert.Equal(0x6e, TurzxMediaProtocol.FileSize(path + "test.mp4")[0]);
    }

    [Theory]
    [InlineData("/root/video/../config")]
    [InlineData("/root/video/./test")]
    [InlineData("/root/video/é.mp4")]
    [InlineData("/root/video/test\n.mp4")]
    [InlineData("/root/video-other/test")]
    [InlineData("/root/img/test")]
    [InlineData("")]
    public void OtherPathsAreRejectedBeforeSerialUse(string path)
        => Assert.Throws<ArgumentException>(() => TurzxMediaProtocol.ListVideos(path));
}
