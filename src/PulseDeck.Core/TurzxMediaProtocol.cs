// SPDX-License-Identifier: GPL-3.0-or-later
// Experimental revision-C media commands described in video-support PR #348
// by gwendal-h. Not used by production display delivery.
using System.Text;
using System.Buffers.Binary;

namespace PulseDeck.Core;

public static class TurzxMediaProtocol
{
    public static byte[] ListVideos(string directory) => PathCommand(0x65, directory);
    public static byte[] FileSize(string path) => PathCommand(0x6e, path);
    public static byte[] StartVideo(string path) => PathCommand(0x78, path, options: 1);
    public static byte[] DeleteVideo(string path) => PathCommand(0x66, path);
    public static byte[] UploadVideo(string path, int size)
    {
        if (size is < 1 or > 131072) throw new ArgumentOutOfRangeException(nameof(size));
        var packet = PathCommand(0x6f, path);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(10 + path.Length, 4), size);
        return packet;
    }

    private static byte[] PathCommand(byte operation, string path, byte options = 0)
    {
        if (string.IsNullOrEmpty(path) || path.Length > 200 || path.Any(c => c < 32 || c > 126)
            || !(path.StartsWith("/root/video/", StringComparison.Ordinal)
                || path.StartsWith("/mnt/SDCARD/video/", StringComparison.Ordinal))
            || path.Split('/').Any(segment => segment is "." or ".."))
            throw new ArgumentException("Expected a bounded ASCII video-storage path.", nameof(path));
        var packet = new byte[250];
        packet[0] = operation; packet[1] = 0xef; packet[2] = 0x69;
        packet[6] = (byte)path.Length;
        packet[7] = options; // Vendor StartVideo noSaveConfig flag: preserve boot media.
        Encoding.ASCII.GetBytes(path).CopyTo(packet, 10);
        return packet;
    }
}
