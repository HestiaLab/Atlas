using System.Xml;
using System.Xml.Schema;
using Xunit;

namespace Atlas.Core.Tests.Persistence;

public sealed class PersistenceSchemaTests
{
    [Fact]
    public void V2_schema_compiles_and_accepts_the_approved_golden_document()
    {
        var errors = Validate(GoldenPath("workspace-v2.xml"));

        Assert.Empty(errors);
    }

    [Fact]
    public void V2_schema_rejects_unknown_attributes()
    {
        var xml = File.ReadAllText(GoldenPath("workspace-v2.xml"))
            .Replace(
                "workspaceId=\"main\"",
                "workspaceId=\"main\" unknown=\"rejected\"",
                StringComparison.Ordinal);

        var errors = ValidateText(xml);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void V2_schema_rejects_non_semantic_schema_version()
    {
        var xml = File.ReadAllText(GoldenPath("workspace-v2.xml"))
            .Replace("schemaVersion=\"2.0\"", "schemaVersion=\"2\"", StringComparison.Ordinal);

        var errors = ValidateText(xml);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void V1_golden_is_the_approved_narrow_root_and_version()
    {
        using var reader = XmlReader.Create(
            GoldenPath("workspace-v1-narrow.xml"),
            SecureReaderSettings());
        reader.MoveToContent();

        Assert.Equal("AtlasLayout", reader.LocalName);
        Assert.Equal(string.Empty, reader.NamespaceURI);
        Assert.Equal("1.0", reader.GetAttribute("schemaVersion"));
    }

    private static IReadOnlyList<string> Validate(string path)
    {
        using var reader = XmlReader.Create(path, SecureReaderSettings());
        return Validate(reader);
    }

    private static IReadOnlyList<string> ValidateText(string xml)
    {
        using var stringReader = new StringReader(xml);
        using var reader = XmlReader.Create(stringReader, SecureReaderSettings());
        return Validate(reader);
    }

    private static IReadOnlyList<string> Validate(XmlReader source)
    {
        var schemas = new XmlSchemaSet
        {
            XmlResolver = null,
        };
        schemas.Add("urn:atlas:workspace:2", SchemaPath());

        var errors = new List<string>();
        var settings = SecureReaderSettings();
        settings.ValidationType = ValidationType.Schema;
        settings.Schemas = schemas;
        settings.ValidationEventHandler += (_, args) => errors.Add(args.Message);

        using var validatingReader = XmlReader.Create(source, settings);
        while (validatingReader.Read())
        {
        }

        return errors;
    }

    private static XmlReaderSettings SecureReaderSettings()
    {
        return new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
    }

    private static string SchemaPath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Persistence",
            "Schema",
            "atlas-workspace-v2.xsd");
    }

    private static string GoldenPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Persistence", "Golden", fileName);
    }
}
