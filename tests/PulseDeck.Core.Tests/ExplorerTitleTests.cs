using PulseDeck.Core;
using Xunit;

public sealed class ExplorerTitleTests
{
    [Theory]
    [InlineData("explorer", "CabinetWClass", "Documenti", "Documenti")]
    [InlineData("explorer", "CabinetWClass", "Download", "Download")]
    [InlineData("EXPLORER", "ExploreWClass", "Questo PC", "Questo PC")]
    [InlineData("explorer", "CabinetWClass", @"C:\Users\Example\Projects\PulseDeck", "PulseDeck")]
    [InlineData("explorer", "CabinetWClass", @"C:\Users\Example\Projects\PulseDeck\", "PulseDeck")]
    [InlineData("explorer", "CabinetWClass", @"C:\", @"C:\")]
    [InlineData("explorer", "CabinetWClass", @"\\server\share\Folder", "Folder")]
    [InlineData("explorer", "CabinetWClass", @"\\server\share\", "share")]
    [InlineData("explorer", "CabinetWClass", "", null)]
    [InlineData("explorer", "Progman", "Program Manager", null)]
    [InlineData("explorer", "WorkerW", "Desktop", null)]
    [InlineData("explorer", "Shell_TrayWnd", "", null)]
    [InlineData("OtherApp", "CabinetWClass", "Documenti", null)]
    public void FolderWindowsUseSelectedTitleWithoutLeakingDesktopOrParentPaths(string process,
        string windowClass, string title, string? expected)
        => Assert.Equal(expected, ForegroundTitles.FromWindow(process, title, windowClass));
}
