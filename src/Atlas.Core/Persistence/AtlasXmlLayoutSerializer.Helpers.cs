using System.Globalization;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Atlas.Core.Validation;

namespace Atlas.Core.Persistence;

/// <summary>
///     <see cref="AtlasXmlLayoutSerializer"/> 的辅助成员（partial）。
/// </summary>
public sealed partial class AtlasXmlLayoutSerializer
{
    private static void EnsureSnapshotValid(WorkspaceSnapshot snapshot)
    {
        if (WorkspaceInvariantValidator.Validate(snapshot).Count != 0)
        {
            throw new ArgumentException(
                "The snapshot violates committed-state invariants.",
                nameof(snapshot));
        }
    }

    private static void EnsureSnapshotWithinLimits(
        WorkspaceSnapshot snapshot,
        XDocument document)
    {
        var limits = new LayoutPersistenceLimits();
        foreach (var group in snapshot.Nodes.Values.OfType<GroupNode>())
        {
            if (group.ItemIds.Count > limits.MaxItemsPerGroup
                || group.EditorState?.RecentlyVisitedIds.Count > limits.MaxItemsPerGroup)
            {
                throw new ArgumentException(
                    "The snapshot exceeds the persistence group item limit.",
                    nameof(snapshot));
            }
        }

        if (snapshot.ToolBarOrders.Values.Any(
                order => order.ItemIds.Count > limits.MaxItemsPerGroup)
            || snapshot.AutoHideStrips.Values.Any(
                strip => strip.Entries.Count > limits.MaxItemsPerGroup))
        {
            throw new ArgumentException(
                "The snapshot exceeds the persistence collection item limit.",
                nameof(snapshot));
        }

        try
        {
            ValidateLimits(document.Root!, limits);
        }
        catch (XmlException exception)
        {
            throw new ArgumentException(
                "The snapshot exceeds the persistence size limits.",
                nameof(snapshot),
                exception);
        }
    }

    private static void EnsureDocumentWithinLimit(string xml)
    {
        var limits = new LayoutPersistenceLimits();
        if (System.Text.Encoding.UTF8.GetByteCount(xml) > limits.MaxDocumentBytes)
        {
            throw new ArgumentException(
                "The serialized snapshot exceeds the persistence byte limit.",
                "snapshot");
        }
    }

    private static bool TryCompatibleVersion(string? value)
    {
        if (value is null
            || !Version.TryParse(value, out var version)
            || version.Major != 2
            || value.Count(character => character == '.') != 1)
        {
            return false;
        }

        return true;
    }

    private static string Required(XElement element, string name)
    {
        return (string?)element.Attribute(name)
            ?? throw new XmlException($"Required attribute '{name}' is missing.");
    }

    private static string? Optional(XElement element, string name)
    {
        return (string?)element.Attribute(name);
    }

    private static XAttribute? OptionalAttribute(string name, string? value)
    {
        return value is null ? null : new XAttribute(name, value);
    }

    private static string? Format(double? value)
    {
        return value?.ToString("R", CultureInfo.InvariantCulture);
    }

    private static T ParseEnum<T>(string value)
        where T : struct, Enum
    {
        return Enum.TryParse<T>(value, ignoreCase: false, out var parsed)
            && Enum.IsDefined(typeof(T), parsed)
                ? parsed
                : throw new XmlException($"Unknown {typeof(T).Name} value.");
    }

    private static bool ParseBool(string value)
    {
        return XmlConvert.ToBoolean(value);
    }

    private static int ParseInt(string value)
    {
        return XmlConvert.ToInt32(value);
    }

    private static double ParseFinite(string value)
    {
        var parsed = XmlConvert.ToDouble(value);
        return double.IsFinite(parsed)
            ? parsed
            : throw new XmlException("A numeric value must be finite.");
    }

    private static double? ParseNullableFinite(string? value)
    {
        return value is null ? null : ParseFinite(value);
    }

    private static WorkspaceResult<LayoutRestoreDocument> Invalid(string diagnosticId)
    {
        return WorkspaceResult<LayoutRestoreDocument>.Failure(
            new WorkspaceError(
                WorkspaceErrorCode.InvalidLayoutData,
                LayoutOperationKind.RestoreWorkspace,
                diagnosticId,
                "Atlas.Persistence.InvalidLayoutData"));
    }

    private static WorkspaceResult<LayoutRestoreDocument> InvalidVersion()
    {
        return WorkspaceResult<LayoutRestoreDocument>.Failure(
            new WorkspaceError(
                WorkspaceErrorCode.LayoutVersionIncompatible,
                LayoutOperationKind.RestoreWorkspace,
                "ATLAS-PERSIST-VERSION",
                "Atlas.Persistence.VersionIncompatible"));
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        internal Utf8StringWriter(IFormatProvider formatProvider)
            : base(formatProvider)
        {
        }

        public override Encoding Encoding => Encoding.UTF8;
    }

    private interface ILegacyItem
    {
        DockItemId Id { get; }

        bool Active { get; }
    }

}
