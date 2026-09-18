using PulseDeck.Agent.Providers;
using SkiaSharp;
using Xunit;

public sealed class TerminalIconTests
{
    [Fact]
    public void WslTitleUsesInstalledAssetAndOtherTabsNeverReuseIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "pulsedeck-terminal-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "ProfileIcons"));
        try
        {
            using var bitmap = new SKBitmap(16, 16);
            bitmap.Erase(SKColors.Green);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            var bytes = data.ToArray();
            File.WriteAllBytes(Path.Combine(root, "ProfileIcons", "{9acb9455-ca41-5af7-950f-6bca1bc9722f}.scale-200.png"), bytes);
            var provider = new TerminalIcon();
            var executable = Path.Combine(root, "WindowsTerminal.exe");
            Assert.Equal(bytes, provider.Read(executable, "WSL tmux - pulsedeck-codex")!.Png);
            foreach (var title in new[] { "PowerShell", "WSLTools", "Project WSL", "" })
                Assert.Null(provider.Read(executable, title));
            Assert.Equal(bytes, provider.Read(executable, "wsl: Ubuntu")!.Png);
            Assert.Null(provider.Read(Path.Combine(root, "missing", "WindowsTerminal.exe"), "WSL"));
        }
        finally { Directory.Delete(root, true); }
    }
}
