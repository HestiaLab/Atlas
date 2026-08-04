using System.Globalization;
using Atlas.Demo.Models.Dbc;

namespace Atlas.Demo.Services;

/// <summary>
/// Hand-written DBC (CAN database) parser and serializer with round-trip
/// preservation. Parsing tolerates the whitespace variations found in the
/// wild (single- or double-space <c>SG_</c> indent, CRLF or LF line endings,
/// optional blank lines); serialization emits the canonical DBC layout
/// (two-space <c>SG_</c> indent, blank line between messages).
/// </summary>
public static class DbcParser
{
    public static DbcDocument Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var lines = SplitLines(content);
        var nodes = Array.Empty<string>();
        var headerSections = new List<string>();
        var messages = new List<DbcMessage>();
        var trailingSections = new List<string>();
        var inHeader = true;
        DbcMessageBuilder? current = null;
        var version = string.Empty;

        foreach (var line in lines)
        {
            if (inHeader)
            {
                if (line.StartsWith("VERSION", StringComparison.Ordinal))
                {
                    version = ExtractVersion(line);
                    continue;
                }

                if (line.StartsWith("BU_:", StringComparison.Ordinal))
                {
                    nodes = ParseNodes(line);
                    inHeader = false;
                    continue;
                }

                headerSections.Add(line);
                continue;
            }

            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("BO_", StringComparison.Ordinal))
            {
                current = new DbcMessageBuilder(ParseMessageHeader(trimmed));
                messages.Add(current.Message);
            }
            else if (current is not null && trimmed.StartsWith("SG_", StringComparison.Ordinal))
            {
                current.Signals.Add(ParseSignal(trimmed));
            }
            else
            {
                trailingSections.Add(line);
            }
        }

        return new DbcDocument(
            version,
            nodes,
            string.Join("\n", headerSections),
            messages,
            string.Join("\n", trailingSections));
    }

    public static string Serialize(DbcDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var writer = new List<string>();

        writer.Add($"VERSION \"{doc.Version}\"");
        if (doc.HeaderSections.Length > 0)
        {
            writer.Add(doc.HeaderSections);
        }

        writer.Add($"BU_: {string.Join(' ', doc.Nodes)}");

        for (var i = 0; i < doc.Messages.Count; i++)
        {
            if (i > 0)
            {
                writer.Add(string.Empty);
            }

            var message = doc.Messages[i];
            writer.Add(
                $"BO_ {message.Id} {message.Name}: {message.Length} {message.Transmitter}");
            foreach (var signal in message.Signals)
            {
                writer.Add("  " + SerializeSignal(signal));
            }
        }

        if (doc.TrailingSections.Length > 0)
        {
            writer.Add(doc.TrailingSections);
        }

        return string.Join("\n", writer);
    }

    private static string[] SplitLines(string content)
    {
        // Normalize CRLF/CR to LF, drop a single trailing empty entry so a
        // file ending with a newline does not produce a phantom blank line.
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized.Split('\n');
        if (lines.Length > 0 && lines[^1].Length == 0)
        {
            Array.Resize(ref lines, lines.Length - 1);
        }

        return lines;
    }

    private static string ExtractVersion(string line)
    {
        var firstQuote = line.IndexOf('"');
        if (firstQuote < 0)
        {
            return string.Empty;
        }

        var secondQuote = line.IndexOf('"', firstQuote + 1);
        if (secondQuote < 0)
        {
            return line[(firstQuote + 1)..];
        }

        return line[(firstQuote + 1)..secondQuote];
    }

    private static string[] ParseNodes(string line)
    {
        var rest = line["BU_:".Length..].Trim();
        return rest.Length == 0
            ? Array.Empty<string>()
            : rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static (uint Id, string Name, byte Length, string Transmitter) ParseMessageHeader(
        string line)
    {
        var rest = line["BO_".Length..].Trim();
        var idEnd = rest.IndexOf(' ');
        var id = uint.Parse(rest[..idEnd], CultureInfo.InvariantCulture);
        rest = rest[(idEnd + 1)..].Trim();

        var colon = rest.IndexOf(':');
        var name = rest[..colon].Trim();
        rest = rest[(colon + 1)..].Trim();

        var lengthEnd = rest.IndexOf(' ');
        var length = byte.Parse(
            lengthEnd < 0 ? rest : rest[..lengthEnd],
            CultureInfo.InvariantCulture);
        var transmitter = lengthEnd < 0 ? string.Empty : rest[(lengthEnd + 1)..].Trim();

        return (id, name, length, transmitter);
    }

    private static DbcSignal ParseSignal(string line)
    {
        var rest = line["SG_".Length..].Trim();

        var colon = rest.IndexOf(':');
        var name = rest[..colon].Trim();
        rest = rest[(colon + 1)..].Trim();

        // <start>|<len>@<byteorder><sign> (<factor>,<offset>) [<min>|<max>] "<unit>" <receiver>
        var layoutEnd = rest.IndexOf(' ');
        var layout = rest[..layoutEnd];
        rest = rest[(layoutEnd + 1)..].Trim();

        var startBitEnd = layout.IndexOf('|');
        var startBit = int.Parse(layout[..startBitEnd], CultureInfo.InvariantCulture);
        var lenEnd = layout.IndexOf('@');
        var length = int.Parse(layout[(startBitEnd + 1)..lenEnd], CultureInfo.InvariantCulture);
        var byteOrder = layout[lenEnd + 1];
        var isBigEndian = byteOrder == '1';
        var isSigned = layout.Length > lenEnd + 2 && layout[lenEnd + 2] == '-';

        var factor = 0.0;
        var offset = 0.0;
        var min = 0.0;
        var max = 0.0;
        var unit = string.Empty;

        if (rest.StartsWith("(", StringComparison.Ordinal))
        {
            var factorEnd = rest.IndexOf(',');
            factor = double.Parse(
                rest[1..factorEnd],
                NumberStyles.Float,
                CultureInfo.InvariantCulture);
            var offsetEnd = rest.IndexOf(')');
            offset = double.Parse(
                rest[(factorEnd + 1)..offsetEnd],
                NumberStyles.Float,
                CultureInfo.InvariantCulture);
            rest = rest[(offsetEnd + 1)..].Trim();
        }

        if (rest.StartsWith("[", StringComparison.Ordinal))
        {
            var minEnd = rest.IndexOf('|');
            min = double.Parse(
                rest[1..minEnd],
                NumberStyles.Float,
                CultureInfo.InvariantCulture);
            var maxEnd = rest.IndexOf(']');
            max = double.Parse(
                rest[(minEnd + 1)..maxEnd],
                NumberStyles.Float,
                CultureInfo.InvariantCulture);
            rest = rest[(maxEnd + 1)..].Trim();
        }

        if (rest.StartsWith("\"", StringComparison.Ordinal))
        {
            var unitEnd = rest.IndexOf('"', 1);
            unit = unitEnd < 0 ? rest[1..] : rest[1..unitEnd];
            rest = unitEnd < 0 ? string.Empty : rest[(unitEnd + 1)..].Trim();
        }

        return new DbcSignal(
            name,
            startBit,
            length,
            isBigEndian,
            isSigned,
            factor,
            offset,
            min,
            max,
            unit,
            rest);
    }

    private static string SerializeSignal(DbcSignal signal)
    {
        var byteOrder = signal.IsBigEndian ? "1" : "0";
        var sign = signal.IsSigned ? "-" : "+";
        var layout = $"{signal.StartBit}|{signal.Length}@{byteOrder}{sign}";
        var factorOffset =
            $"({FormatNumber(signal.Factor)},{FormatNumber(signal.Offset)})";
        var range =
            $"[{FormatNumber(signal.Min)}|{FormatNumber(signal.Max)}]";
        return
            $"SG_ {signal.Name} : {layout} {factorOffset} {range} \"{signal.Unit}\" {signal.Receiver}";
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private sealed class DbcMessageBuilder
    {
        public DbcMessageBuilder(
            (uint Id, string Name, byte Length, string Transmitter) header)
        {
            Message = new DbcMessage(
                header.Id,
                header.Name,
                header.Length,
                header.Transmitter,
                Signals);
        }

        public DbcMessage Message { get; }

        public List<DbcSignal> Signals { get; } = new();
    }
}
