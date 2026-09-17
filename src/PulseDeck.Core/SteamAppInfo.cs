using System.Text;

namespace PulseDeck.Core;

public sealed record SteamLaunchInfo(string Type, string[] Executables);

// Steam's local appinfo cache is optional and versioned. Unknown/corrupt data is
// rejected by the caller, which retains manifest-based discovery for review.
public static class SteamAppInfo
{
    private sealed record Node(string? Value, Dictionary<string, Node> Children);
    public static Dictionary<string, SteamLaunchInfo> Read(byte[] bytes)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8);
        var magic = reader.ReadUInt32();
        if (magic is not (0x07564427 or 0x07564428 or 0x07564429)) throw new InvalidDataException("Unknown Steam appinfo version.");
        reader.ReadUInt32();
        string[]? keys = null;
        var recordsEnd = (long)bytes.Length;
        if (magic == 0x07564429)
        {
            recordsEnd = checked((long)reader.ReadUInt64());
            if (recordsEnd < 16 || recordsEnd > bytes.Length - 4) throw new InvalidDataException();
            reader.BaseStream.Position = recordsEnd;
            var count = reader.ReadUInt32();
            if (count > 100000) throw new InvalidDataException();
            keys = Enumerable.Range(0, (int)count).Select(_ => ReadString(reader, bytes.Length)).ToArray();
            reader.BaseStream.Position = 16;
        }
        var result = new Dictionary<string, SteamLaunchInfo>();
        while (reader.BaseStream.Position + 4 <= recordsEnd)
        {
            var id = reader.ReadUInt32();
            if (id == 0) break;
            var size = reader.ReadUInt32();
            var end = reader.BaseStream.Position + size;
            if (size > 16 * 1024 * 1024 || end > recordsEnd) throw new InvalidDataException();
            reader.BaseStream.Position += magic == 0x07564427 ? 40 : 60;
            var nodes = 0;
            var tree = ReadNodes(reader, keys, end, 0, ref nodes);
            if (tree.TryGetValue("appinfo", out var app))
            {
                var type = Child(app, "common", "type")?.Value ?? "";
                var launch = Child(app, "config", "launch");
                var executables = launch?.Children.Values.Where(n =>
                {
                    var os = Child(n, "config", "oslist")?.Value;
                    return os is null || os.Split(',').Contains("windows", StringComparer.OrdinalIgnoreCase);
                }).Select(n => Child(n, "executable")?.Value).OfType<string>()
                    .Where(p => p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
                result[id.ToString()] = new(type, executables);
            }
            reader.BaseStream.Position = end;
        }
        return result;
    }
    private static Node? Child(Node node, params string[] path)
    {
        foreach (var key in path) { if (!node.Children.TryGetValue(key, out var child)) return null; node = child; }
        return node;
    }
    private static Dictionary<string, Node> ReadNodes(BinaryReader reader, string[]? keys, long end, int depth, ref int count)
    {
        if (depth > 32) throw new InvalidDataException();
        var result = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        while (reader.BaseStream.Position < end)
        {
            if (++count > 100000) throw new InvalidDataException();
            var type = reader.ReadByte();
            if (type == 8) return result;
            var key = keys is null ? ReadString(reader, end) : keys[checked((int)reader.ReadUInt32())];
            if (type == 0) result[key] = new(null, ReadNodes(reader, keys, end, depth + 1, ref count));
            else if (type == 1) result[key] = new(ReadString(reader, end), new());
            else if (type is 2 or 3 or 4 or 6) reader.ReadUInt32();
            else if (type is 7 or 10) reader.ReadUInt64();
            else throw new InvalidDataException("Unsupported Steam value type.");
        }
        throw new InvalidDataException("Truncated Steam object.");
    }
    private static string ReadString(BinaryReader reader, long end)
    {
        var bytes = new List<byte>();
        while (reader.BaseStream.Position < end && bytes.Count < 65536)
        {
            var b = reader.ReadByte(); if (b == 0) return Encoding.UTF8.GetString(bytes.ToArray()); bytes.Add(b);
        }
        throw new InvalidDataException("Truncated Steam string.");
    }
}
