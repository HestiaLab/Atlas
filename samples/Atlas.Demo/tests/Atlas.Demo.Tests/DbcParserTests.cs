#nullable enable
using Atlas.Demo.Models.Dbc;
using Atlas.Demo.Services;
using Xunit;

namespace Atlas.Demo.Tests;

public sealed class DbcParserTests
{
    private const string SampleFragment =
        "VERSION \"\"\n" +
        "\n" +
        "NS_ :\n" +
        "    NS_DESC_\n" +
        "    CM_\n" +
        "    BA_DEF_\n" +
        "    BA_\n" +
        "    VAL_\n" +
        "\n" +
        "BS_:\n" +
        "\n" +
        "BU_: DME SZL XXX\n" +
        "\n" +
        "BO_ 170 AccPedal: 8 DME\n" +
        " SG_ KickDownPressed : 53|1@0+ (1,0) [0|3] \"\" XXX\n" +
        " SG_ EngineSpeed : 32|16@1- (1,0) [0|65535] \"\" XXX\n" +
        " SG_ AccelPct : 16|16@1+ (0.01,0) [0|100] \"%\" Vector__XXX\n" +
        "\n" +
        "BO_ 404 CruiseControl: 4 SZL\n" +
        " SG_ CruiseActive : 0|1@0+ (1,0) [0|1] \"\" XXX\n";

    [Fact]
    public void Parse_reads_messages_and_signals_from_fragment()
    {
        var doc = DbcParser.Parse(SampleFragment);

        Assert.Equal(2, doc.Messages.Count);

        var pedal = doc.Messages[0];
        Assert.Equal(170u, pedal.Id);
        Assert.Equal("AccPedal", pedal.Name);
        Assert.Equal(8, pedal.Length);
        Assert.Equal("DME", pedal.Transmitter);
        Assert.Equal(3, pedal.Signals.Count);

        var engineSpeed = pedal.Signals[1];
        Assert.Equal("EngineSpeed", engineSpeed.Name);
        Assert.Equal(32, engineSpeed.StartBit);
        Assert.Equal(16, engineSpeed.Length);
        Assert.True(engineSpeed.IsBigEndian);
        Assert.Equal(1.0, engineSpeed.Factor);
        Assert.Equal(0.0, engineSpeed.Offset);
        Assert.Equal(0.0, engineSpeed.Min);
        Assert.Equal(65535.0, engineSpeed.Max);
        Assert.Equal("", engineSpeed.Unit);
        Assert.Equal("XXX", engineSpeed.Receiver);

        var accelPct = pedal.Signals[2];
        Assert.True(accelPct.IsBigEndian);
        Assert.False(accelPct.IsSigned);
        Assert.Equal(0.01, accelPct.Factor);
        Assert.Equal("%", accelPct.Unit);
        Assert.Equal("Vector__XXX", accelPct.Receiver);

        var cruise = doc.Messages[1];
        Assert.Equal(404u, cruise.Id);
        Assert.Equal("CruiseControl", cruise.Name);
        Assert.Equal(4, cruise.Length);
        Assert.Equal("SZL", cruise.Transmitter);
        Assert.Single(cruise.Signals);
    }

    [Fact]
    public void Parse_then_Serialize_round_trips_acceptance_sample()
    {
        var original = File.ReadAllText(Path.Combine("Samples", "bmw_e9x_e8x.dbc"));

        var doc = DbcParser.Parse(original);
        var serialized = DbcParser.Serialize(doc);

        Assert.Equal(325, doc.Messages.Count);
        Assert.Equal(38, CountLines(original, "CM_"));
        Assert.Equal(3, CountLines(original, "VAL_"));
        Assert.Contains("Vector__XXX", doc.Nodes);
        Assert.Equal(NormalizeLines(original), NormalizeLines(serialized));
    }

    [Fact]
    public void Serialize_emits_canonical_message_and_signal_layout()
    {
        var doc = DbcParser.Parse(SampleFragment);
        var serialized = DbcParser.Serialize(doc);

        var lines = serialized.Split('\n');
        var bo170 = Array.FindIndex(lines, l => l.StartsWith("BO_ 170", StringComparison.Ordinal));
        var bo404 = Array.FindIndex(lines, l => l.StartsWith("BO_ 404", StringComparison.Ordinal));

        Assert.True(bo170 >= 0);
        Assert.True(bo404 >= 0);

        // Canonical layout: BO_ line immediately followed by two-space-indented
        // SG_ lines, and a blank line between messages.
        Assert.StartsWith("  SG_ ", lines[bo170 + 1], StringComparison.Ordinal);
        Assert.Contains("  SG_ ", serialized, StringComparison.Ordinal);
        Assert.Equal(string.Empty, lines[bo404 - 1]);
    }

    [Fact]
    public void Parse_handles_edge_cases()
    {
        var empty = DbcParser.Parse("VERSION \"\"\nBU_: XXX\n");
        Assert.Empty(empty.Messages);
        Assert.Single(empty.Nodes);

        var noSignals = DbcParser.Parse(
            "VERSION \"\"\nBU_: XXX\n\nBO_ 100 EmptyMsg: 1 XXX\n");
        Assert.Single(noSignals.Messages);
        Assert.Empty(noSignals.Messages[0].Signals);
    }

    private static int CountLines(string content, string prefix)
    {
        return content.Split('\n').Count(l => l.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Whitespace-insensitive comparison for round-trip verification: line
    /// endings, leading indentation, and blank lines are normalized away while
    /// preserving order and all non-whitespace content.
    /// </summary>
    private static string[] NormalizeLines(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();
    }
}
