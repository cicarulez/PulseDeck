// SPDX-License-Identifier: GPL-3.0-or-later
// Experimental composition contract: gwendal-h's public video-overlay PR #348,
// combined with the already verified 8.8-inch BGRA format. Not used by the agent.
using System.Buffers.Binary;

namespace PulseDeck.Core;

public static class TurzxVideoOverlayProtocol
{
    public static byte[] InitialiseCommand() => TurzxProtocol.Packet(Convert.FromHexString("CAEF690038400E10"));
    public static byte[] EmptyVisibilityCommand() => TurzxProtocol.Packet(Convert.FromHexString("D0EF6900000002"));

    // One small opaque rectangle: rewrite its background too, so old glyphs vanish.
    // The visibility map declares only that rectangle over the internally played video.
    public static (byte[] Header, byte[] Payload) Frame(byte[] pixels, FrameRect rect, uint counter)
    {
        if (pixels.Length != TurzxProtocol.Width * TurzxProtocol.Height * 4) throw new ArgumentException("Expected landscape BGRA.");
        if (rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0
            || rect.Width > 1920 || rect.Height > 480 || rect.X > 1920 - rect.Width || rect.Y > 480 - rect.Height
            || (long)rect.Width * rect.Height > 20000) throw new ArgumentOutOfRangeException(nameof(rect));
        using var raw = new MemoryStream();
        void Segment(int x)
        {
            var address = x * 480 + 480 - rect.Y - rect.Height;
            raw.WriteByte((byte)(address >> 16)); raw.WriteByte((byte)(address >> 8)); raw.WriteByte((byte)address);
            raw.WriteByte((byte)(rect.Height >> 8)); raw.WriteByte((byte)rect.Height);
        }
        for (int x = rect.X; x < rect.X + rect.Width; x++)
        {
            Segment(x);
            for (int y = rect.Y + rect.Height - 1; y >= rect.Y; y--)
            {
                var pixel = pixels.AsSpan((y * 1920 + x) * 4, 4);
                if (pixel[3] != 255) throw new ArgumentException("The diagnostic overlay must be opaque inside its rectangle.");
                raw.Write(pixel);
            }
        }
        var visibilityBytes = rect.Width * 5;
        for (int x = rect.X; x < rect.X + rect.Width; x++) Segment(x);
        var bytes = raw.ToArray();
        using var stuffed = new MemoryStream();
        for (int i = 0; i < bytes.Length; i += 249)
        {
            if (i > 0) stuffed.WriteByte(0);
            stuffed.Write(bytes.AsSpan(i, Math.Min(249, bytes.Length - i)));
        }
        stuffed.Write([0xef, 0x69]);
        var size = bytes.Length + 2;
        var header = new byte[18];
        header[0] = 0xcc; header[1] = 0xef; header[2] = 0x69;
        header[4] = (byte)(size >> 16); header[5] = (byte)(size >> 8); header[6] = (byte)size;
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(10), counter);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(14), (uint)visibilityBytes);
        return (TurzxProtocol.Packet(header), TurzxProtocol.Packet(stuffed.ToArray()));
    }
}
