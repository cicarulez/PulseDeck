// SPDX-License-Identifier: GPL-3.0-or-later
// Protocol/encoding adapted from turing-smart-screen-python, lcd_comm_rev_c.py.
// Copyright (C) 2021 Matthieu Houdebine; 2023 Alex W. Baule, Arthur Ferrai.
// See THIRD-PARTY-NOTICES.md and LICENSE.
using System.Buffers.Binary;

namespace PulseDeck.Core;

public static class TurzxProtocol
{
    public const int Width = 1920, Height = 480;
    // Captured from the successful upstream 8.8-inch probe: 4-byte command,
    // 2-byte frame size, 2-byte line size. No extra zero before 0x3840.
    public static byte[] FullFrameCommand() => Packet(Convert.FromHexString("C8EF690038400E10"));
    public static byte[] Packet(ReadOnlySpan<byte> data, byte padding = 0)
    {
        var result = new byte[((data.Length + 249) / 250) * 250];
        if (padding != 0) Array.Fill(result, padding);
        data.CopyTo(result);
        return result;
    }

    private static byte[] Stuff(ReadOnlySpan<byte> raw, bool terminator)
    {
        using var stream = new MemoryStream();
        for (int offset = 0; offset < raw.Length; offset += 249)
        {
            if (offset > 0) stream.WriteByte(0);
            stream.Write(raw.Slice(offset, Math.Min(249, raw.Length - offset)));
        }
        if (terminator) stream.Write([0xef, 0x69]);
        return Packet(stream.ToArray());
    }

    public static byte[] FullFrame(byte[] landscapeBgra)
    {
        CheckFrame(landscapeBgra);
        var portrait = new byte[landscapeBgra.Length];
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
            landscapeBgra.AsSpan((y * Width + x) * 4, 4).CopyTo(portrait.AsSpan((x * Height + Height - y - 1) * 4, 4));
        return Stuff(portrait, false);
    }

    public static (byte[] Header, byte[] Payload) PartialFrame(byte[] landscapeBgra, FrameRect rect, int rom, uint count)
    {
        CheckFrame(landscapeBgra);
        if (rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0 || rect.X + rect.Width > Width || rect.Y + rect.Height > Height)
            throw new ArgumentOutOfRangeException(nameof(rect));
        using var raw = new MemoryStream();
        var pixelBytes = rom > 88 ? 4 : 3;
        for (int x = rect.X; x < rect.X + rect.Width; x++)
        {
            var address = x * Height + Height - rect.Y - rect.Height;
            raw.WriteByte((byte)(address >> 16)); raw.WriteByte((byte)(address >> 8)); raw.WriteByte((byte)address);
            raw.WriteByte((byte)(rect.Height >> 8)); raw.WriteByte((byte)rect.Height);
            for (int y = rect.Y + rect.Height - 1; y >= rect.Y; y--)
                raw.Write(landscapeBgra.AsSpan((y * Width + x) * 4, pixelBytes));
        }
        var size = (int)raw.Length + 2;
        byte[] header = [0xcc, 0xef, 0x69, 0, (byte)(size >> 16), (byte)(size >> 8), (byte)size, 0, 0, 0, 0, 0, 0, 0];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(10), count);
        return (Packet(header), Stuff(raw.ToArray(), true));
    }

    public static FrameRect? ChangedRegion(byte[] before, byte[] after)
    {
        CheckFrame(before); CheckFrame(after);
        int minX = Width, minY = Height, maxX = -1, maxY = -1;
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            int offset = (y * Width + x) * 4;
            if (before.AsSpan(offset, 4).SequenceEqual(after.AsSpan(offset, 4))) continue;
            minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
        }
        return maxX < 0 ? null : new(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static void CheckFrame(byte[] pixels)
    {
        if (pixels.Length != Width * Height * 4) throw new ArgumentException("Expected a 1920x480 BGRA frame.");
    }
}

public readonly record struct FrameRect(int X, int Y, int Width, int Height);
