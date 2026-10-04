using System.Xml;
using System.Xml.Linq;

namespace PulseDeck.Core;

public static class AuraPower
{
    // Observed LightingService LastProfile.xml: Group/isenabled changes 1 -> 0
    // for Dark (OFF), while the passive HAL retains its last RGB frame.
    public static bool? ReadOff(string xml)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 262144 });
            var root = XDocument.Load(reader).Root;
            if (root?.Name != "root" || root.Element("header")?.Value != "ASUS_AURA") return null;
            var external = root.Elements("ingroupdevice").Where(e => (string?)e.Attribute("key") == "EXTERNAL_GENERAL").ToArray();
            if (external.Length != 1 || !uint.TryParse(external[0].Value, out _)) return null;
            var groups = root.Elements("device").Where(e => (string?)e.Attribute("key") == "Group").ToArray();
            if (groups.Length != 1) return null;
            var values = groups[0].Elements("isenabled").ToArray();
            return values.Length == 1 ? values[0].Value.Trim() switch { "0" => true, "1" => false, _ => null } : null;
        }
        catch (XmlException) { return null; }
    }

    public static int? Brightness(DeckConfig config, AuraSnapshot aura) =>
        config.AuraEnabled && config.DisplayBrightness is not null && aura.Status == "off" && aura.LightingOff == true
            ? 0 : config.DisplayBrightness;

    public static AuraSnapshot Apply(AuraSnapshot snapshot, bool? off)
    {
        // A healthy receiver may be waiting for its first RGB sample at boot
        // while Aura is already OFF. Its power state can still be read.
        if (snapshot.Status is not ("connected" or "unsupported" or "waiting")) return snapshot;
        return off == true
            ? snapshot with { Status = "off", Color = null, Colors = null, LightingOff = true,
                Detail = "Aura Sync è su Scuro (OFF). Con una luminosità impostata in PulseDeck, il pannello segue lo spegnimento." }
            : snapshot with { LightingOff = off };
    }
}

public sealed class AuraBrightness
{
    private int? restoreBrightness;
    public bool Off { get; private set; }

    public int? Resolve(DeckConfig config, AuraSnapshot aura)
    {
        var off = config.AuraEnabled && config.DisplayBrightness is not null && aura.Status == "off" && aura.LightingOff == true;
        if (off) restoreBrightness = config.DisplayBrightness;
        // Switching to inherited brightness while dark restores the last known
        // manual value once, rather than leaving a temporary zero behind.
        var target = AuraPower.Brightness(config, aura) ?? (Off ? restoreBrightness : null);
        Off = off;
        if (!off) restoreBrightness = null;
        return target;
    }
}
