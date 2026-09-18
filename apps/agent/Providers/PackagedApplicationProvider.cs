using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using PulseDeck.Core;
using SkiaSharp;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace PulseDeck.Agent.Providers;

public sealed class PackagedApplicationProvider
{
    private readonly ApplicationMetadataCache cache = new(Load);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, bool inheritHandle, int processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(SafeProcessHandle process, ref uint length, StringBuilder? value);

    public ApplicationMetadata? Read(int processId)
    {
        try
        {
            using var handle = OpenProcess(0x1000, false, processId);
            if (handle.IsInvalid) return null;
            uint length = 0;
            if (GetApplicationUserModelId(handle, ref length, null) != 122 || length is < 2 or > 512) return null;
            var value = new StringBuilder((int)length);
            return GetApplicationUserModelId(handle, ref length, value) == 0 ? cache.Read(value.ToString()) : null;
        }
        catch { return null; }
    }

    private static async Task<ApplicationMetadata?> Load(string appUserModelId, CancellationToken token)
    {
        var info = AppInfo.GetFromAppUserModelId(appUserModelId).DisplayInfo;
        var name = ForegroundTitles.Clean(info.DisplayName);
        if (name.Length > 120) name = name[..120];
        if (name.Length == 0) name = null;
        ApplicationIcon? icon = null;
        try
        {
            // Windows resolves localized names and package logo variants. No store
            // download, hard-coded branding or copied proprietary assets are needed.
            using var stream = await info.GetLogo(new Size(64, 64)).OpenReadAsync().AsTask(token).ConfigureAwait(false);
            if (stream.Size is >= 8 and <= 524288)
            {
                using var reader = new DataReader(stream);
                var length = (uint)stream.Size;
                if (await reader.LoadAsync(length).AsTask(token).ConfigureAwait(false) == length)
                {
                    var bytes = new byte[length]; reader.ReadBytes(bytes);
                    using var data = SKData.CreateCopy(bytes);
                    using var codec = SKCodec.Create(data);
                    if (codec is not null && codec.Info.Width is > 0 and <= 512 && codec.Info.Height is > 0 and <= 512)
                    {
                        using var bitmap = SKBitmap.Decode(codec);
                        if (bitmap is not null)
                        {
                            using var image = SKImage.FromBitmap(bitmap);
                            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
                            var content = png.ToArray();
                            icon = new(Convert.ToHexString(SHA256.HashData(content)), content);
                        }
                    }
                }
            }
        }
        catch { /* Keep the registered display name even when its logo is unavailable. */ }
        return new(name, icon);
    }
}
