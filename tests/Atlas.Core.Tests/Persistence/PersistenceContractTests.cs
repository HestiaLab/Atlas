using Atlas.Core;
using Atlas.Core.Persistence;
using Xunit;

namespace Atlas.Core.Tests.Persistence;

public sealed class PersistenceContractTests
{
    [Fact]
    public void Preset_descriptor_rejects_invalid_storage_keys_and_revisions()
    {
        Assert.Throws<ArgumentNullException>(
            () => new LayoutPresetDescriptor(null!, "2.0", 0));
        Assert.Throws<ArgumentException>(
            () => new LayoutPresetDescriptor(" ", "2.0", 0));
        Assert.Throws<ArgumentException>(
            () => new LayoutPresetDescriptor(new string('a', 129), "2.0", 0));
        Assert.Throws<ArgumentException>(
            () => new LayoutPresetDescriptor("a\u0001b", "2.0", 0));
        Assert.Throws<ArgumentException>(
            () => new LayoutPresetDescriptor("a/b", "2.0", 0));
        Assert.Throws<ArgumentException>(
            () => new LayoutPresetDescriptor("a\\b", "2.0", 0));
        Assert.Throws<ArgumentException>(
            () => new LayoutPresetDescriptor("__atlas_reserved", "2.0", 0));
        Assert.Throws<ArgumentNullException>(
            () => new LayoutPresetDescriptor("focus", null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LayoutPresetDescriptor("focus", "2.0", -1));

        var descriptor = new LayoutPresetDescriptor("focus", "2.0", 7);

        Assert.Equal("focus", descriptor.Name);
        Assert.Equal("2.0", descriptor.SchemaVersion);
        Assert.Equal(7, descriptor.SavedRevision);
        Assert.Null(descriptor.SavedAt);
    }

    [Fact]
    public void Identity_remap_requires_distinct_identities_and_a_defined_reason()
    {
        Assert.Throws<ArgumentException>(
            () => new IdentityRemap(default, new DockItemId("new"), IdentityRemapReason.Conflict));
        Assert.Throws<ArgumentException>(
            () => new IdentityRemap(new DockItemId("old"), default, IdentityRemapReason.Conflict));
        Assert.Throws<ArgumentException>(
            () => new IdentityRemap(
                new DockItemId("same"),
                new DockItemId("same"),
                IdentityRemapReason.Conflict));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new IdentityRemap(
                new DockItemId("old"),
                new DockItemId("new"),
                (IdentityRemapReason)99));

        var remap = new IdentityRemap(
            new DockItemId("old"),
            new DockItemId("new"),
            IdentityRemapReason.CloneImport);

        Assert.Equal(new DockItemId("old"), remap.OldItemId);
        Assert.Equal(new DockItemId("new"), remap.NewItemId);
        Assert.Equal(IdentityRemapReason.CloneImport, remap.Reason);
    }

    [Fact]
    public void Layout_document_rejects_empty_text_and_negative_revision()
    {
        Assert.Throws<ArgumentNullException>(
            () => new WorkspaceLayoutDocument(null!, 0, "<AtlasWorkspace />"));
        Assert.Throws<ArgumentException>(
            () => new WorkspaceLayoutDocument(" ", 0, "<AtlasWorkspace />"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new WorkspaceLayoutDocument("2.0", -1, "<AtlasWorkspace />"));
        Assert.Throws<ArgumentNullException>(
            () => new WorkspaceLayoutDocument("2.0", 0, null!));
        Assert.Throws<ArgumentException>(
            () => new WorkspaceLayoutDocument("2.0", 0, " "));

        var document = new WorkspaceLayoutDocument("2.0", 3, "<AtlasWorkspace />");

        Assert.Equal("2.0", document.SchemaVersion);
        Assert.Equal(3, document.SavedRevision);
        Assert.Equal("<AtlasWorkspace />", document.Xml);
    }

    [Fact]
    public void Persistence_diagnostic_requires_identity_defined_kind_and_diagnostic_id()
    {
        Assert.Throws<ArgumentException>(
            () => new WorkspacePersistenceDiagnostic(
                default,
                WorkspacePersistenceDiagnosticKind.CorruptDocument,
                "ATLAS-PER-001"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new WorkspacePersistenceDiagnostic(
                new WorkspaceId("main"),
                (WorkspacePersistenceDiagnosticKind)99,
                "ATLAS-PER-001"));
        Assert.Throws<ArgumentNullException>(
            () => new WorkspacePersistenceDiagnostic(
                new WorkspaceId("main"),
                WorkspacePersistenceDiagnosticKind.StorageUnavailable,
                null!));

        var diagnostic = new WorkspacePersistenceDiagnostic(
            new WorkspaceId("main"),
            WorkspacePersistenceDiagnosticKind.SerializationFailure,
            "ATLAS-PER-002");

        Assert.Equal(new WorkspaceId("main"), diagnostic.WorkspaceId);
        Assert.Equal(WorkspacePersistenceDiagnosticKind.SerializationFailure, diagnostic.Kind);
        Assert.Equal("ATLAS-PER-002", diagnostic.DiagnosticId);
    }

    [Fact]
    public void Restore_options_validate_policy_and_reserved_identities()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LayoutRestoreOptions(missingContentPolicy: (MissingContentPolicy)99));
        Assert.Throws<ArgumentException>(
            () => new LayoutRestoreOptions(
                reservedItemIds: new[] { default(DockItemId) }));
        Assert.Throws<ArgumentException>(
            () => new LayoutRestoreOptions(
                reservedItemIds: new[] { new DockItemId("a"), new DockItemId("a") }));

        var defaults = new LayoutRestoreOptions();

        Assert.NotNull(defaults.Limits);
        Assert.Null(defaults.IsContentRegistered);
        Assert.Equal(MissingContentPolicy.PreservePlaceholder, defaults.MissingContentPolicy);
        Assert.Empty(defaults.ReservedItemIds);

        var options = new LayoutRestoreOptions(
            missingContentPolicy: MissingContentPolicy.DropUnknownItems,
            reservedItemIds: new[] { new DockItemId("reserved") });

        Assert.Equal(MissingContentPolicy.DropUnknownItems, options.MissingContentPolicy);
        Assert.Single(options.ReservedItemIds);
    }

    [Fact]
    public void Preset_document_requires_a_descriptor_and_xml()
    {
        var descriptor = new LayoutPresetDescriptor("focus", "2.0", 0);

        Assert.Throws<ArgumentNullException>(
            () => new LayoutPresetDocument(null!, "<AtlasWorkspace />"));
        Assert.Throws<ArgumentNullException>(
            () => new LayoutPresetDocument(descriptor, null!));
        Assert.Throws<ArgumentException>(
            () => new LayoutPresetDocument(descriptor, " "));

        var document = new LayoutPresetDocument(descriptor, "<AtlasWorkspace />");

        Assert.Same(descriptor, document.Descriptor);
        Assert.Equal("<AtlasWorkspace />", document.Xml);
    }
}
