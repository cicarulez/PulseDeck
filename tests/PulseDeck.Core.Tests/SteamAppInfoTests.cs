using System.Text;
using PulseDeck.Core;
using Xunit;

public class SteamAppInfoTests
{
    [Theory]
    [InlineData(0x07564427u)]
    [InlineData(0x07564428u)]
    [InlineData(0x07564429u)]
    public void ReadsDeclaredWindowsLaunchAndSoftwareType(uint magic)
    {
        var info = SteamAppInfo.Read(Fixture(magic));
        Assert.Equal("Game", info["42"].Type);
        Assert.Equal(new[] { @"bin\game.exe" }, info["42"].Executables);
    }
    [Fact]
    public void RejectsUnknownAndTruncatedCaches()
    {
        Assert.Throws<InvalidDataException>(() => SteamAppInfo.Read(new byte[8]));
        var bytes = Fixture(0x07564429);
        Assert.ThrowsAny<Exception>(() => SteamAppInfo.Read(bytes[..^20]));
    }
    private static byte[] Fixture(uint magic)
    {
        var keys = new[] { "appinfo", "common", "type", "config", "launch", "0", "1", "executable", "oslist" };
        using var data = new MemoryStream(); using var writer = new BinaryWriter(data, Encoding.UTF8, true);
        void Str(string value) { writer.Write(Encoding.UTF8.GetBytes(value)); writer.Write((byte)0); }
        void Key(byte type, string name) { writer.Write(type); if (magic == 0x07564429) writer.Write(Array.IndexOf(keys, name)); else Str(name); }
        void Value(string name, string value) { Key(1, name); Str(value); }
        void End() => writer.Write((byte)8);
        Key(0, "appinfo"); Key(0, "common"); Value("type", "Game"); End();
        Key(0, "config"); Key(0, "launch"); Key(0, "0"); Value("executable", @"bin\game.exe"); Key(0, "config"); Value("oslist", "windows"); End(); End();
        Key(0, "1"); Value("executable", "not-windows.exe"); Key(0, "config"); Value("oslist", "linux"); End(); End();
        End(); End(); End(); End();
        using var file = new MemoryStream(); using var output = new BinaryWriter(file, Encoding.UTF8, true);
        output.Write(magic); output.Write(1);
        if (magic == 0x07564429) output.Write((long)0);
        output.Write(42u); var headerSize = magic == 0x07564427 ? 40 : 60;
        output.Write((uint)(headerSize + data.Length)); output.Write(new byte[headerSize]); output.Write(data.ToArray()); output.Write(0u);
        if (magic == 0x07564429)
        {
            var offset = file.Position; output.Write(keys.Length);
            foreach (var key in keys) { output.Write(Encoding.UTF8.GetBytes(key)); output.Write((byte)0); }
            file.Position = 8; output.Write(offset);
        }
        return file.ToArray();
    }
}
