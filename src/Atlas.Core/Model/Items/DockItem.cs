namespace Atlas.Core.Items;

/// <summary>
/// Represents an immutable tool or document item in an Atlas workspace.
/// </summary>
public sealed class DockItem
{
    private const DockItemCapabilities KnownCapabilities =
        DockItemCapabilities.CanClose
        | DockItemCapabilities.CanMove
        | DockItemCapabilities.CanAutoHide
        | DockItemCapabilities.CanSplit
        | DockItemCapabilities.CanPinDocument;

    private const DockItemCapabilities ToolCapabilities =
        DockItemCapabilities.CanClose
        | DockItemCapabilities.CanMove
        | DockItemCapabilities.CanAutoHide;

    private const DockItemCapabilities DocumentCapabilities =
        DockItemCapabilities.CanClose
        | DockItemCapabilities.CanMove
        | DockItemCapabilities.CanSplit
        | DockItemCapabilities.CanPinDocument;

    /// <summary>
    /// Initializes a new instance of the <see cref="DockItem"/> class.
    /// </summary>
    /// <param name="id">The workspace-scoped item identity.</param>
    /// <param name="kind">The item behavioral kind.</param>
    /// <param name="content">The content renderer and resource identity.</param>
    /// <param name="capabilities">The stable capabilities of the item.</param>
    /// <param name="title">
    /// The display title, or <see langword="null"/> to use the item identifier.
    /// </param>
    /// <param name="iconKey">An optional UI-independent icon identity.</param>
    /// <param name="documentState">Document-only committed state.</param>
    public DockItem(
        DockItemId id,
        DockItemKind kind,
        ContentReference content,
        DockItemCapabilities capabilities,
        string? title = null,
        string? iconKey = null,
        DocumentState? documentState = null)
    {
        if (id == default)
        {
            throw new ArgumentException("A dock item identifier cannot be the default value.", nameof(id));
        }

        if (content.IsDefault)
        {
            throw new ArgumentException("A content reference cannot be the default value.", nameof(content));
        }

        if (kind is not DockItemKind.Tool and not DockItemKind.Document)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown dock item kind.");
        }

        ValidateCapabilities(kind, capabilities);
        ValidatePresentation(title, iconKey);

        var resolvedDocumentState = ResolveDocumentState(kind, capabilities, documentState);

        Id = id;
        Kind = kind;
        Content = content;
        Capabilities = capabilities;
        Title = title ?? id.Value;
        IconKey = iconKey;
        DocumentState = resolvedDocumentState;
    }

    /// <summary>
    /// Gets the workspace-scoped item identity.
    /// </summary>
    public DockItemId Id { get; }

    /// <summary>
    /// Gets the item behavioral kind.
    /// </summary>
    public DockItemKind Kind { get; }

    /// <summary>
    /// Gets the display title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the optional UI-independent icon identity.
    /// </summary>
    public string? IconKey { get; }

    /// <summary>
    /// Gets the content renderer and resource identity.
    /// </summary>
    public ContentReference Content { get; }

    /// <summary>
    /// Gets the stable capabilities of the item.
    /// </summary>
    public DockItemCapabilities Capabilities { get; }

    /// <summary>
    /// Gets document-only committed state, or <see langword="null"/> for a tool item.
    /// </summary>
    public DocumentState? DocumentState { get; }

    private static void ValidateCapabilities(
        DockItemKind kind,
        DockItemCapabilities capabilities)
    {
        if ((capabilities & ~KnownCapabilities) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capabilities),
                capabilities,
                "The capability set contains unknown flags.");
        }

        var allowedCapabilities = kind == DockItemKind.Tool
            ? ToolCapabilities
            : DocumentCapabilities;

        if ((capabilities & ~allowedCapabilities) != 0)
        {
            throw new ArgumentException(
                $"The capability set is not valid for a {kind} item.",
                nameof(capabilities));
        }
    }

    private static void ValidatePresentation(string? title, string? iconKey)
    {
        if (title is not null && string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException(
                "A specified title cannot be empty or whitespace.",
                nameof(title));
        }

        if (iconKey is not null && string.IsNullOrWhiteSpace(iconKey))
        {
            throw new ArgumentException(
                "A specified icon key cannot be empty or whitespace.",
                nameof(iconKey));
        }
    }

    private static DocumentState? ResolveDocumentState(
        DockItemKind kind,
        DockItemCapabilities capabilities,
        DocumentState? documentState)
    {
        if (kind == DockItemKind.Tool)
        {
            if (documentState is not null)
            {
                throw new ArgumentException(
                    "Tool items cannot contain document state.",
                    nameof(documentState));
            }

            return null;
        }

        var resolvedState = documentState ?? default;

        if (resolvedState.IsPinned
            && !capabilities.HasFlag(DockItemCapabilities.CanPinDocument))
        {
            throw new ArgumentException(
                "A pinned document must support the pin capability.",
                nameof(documentState));
        }

        if (resolvedState.IsPreview
            && capabilities.HasFlag(DockItemCapabilities.CanPinDocument))
        {
            throw new ArgumentException(
                "A preview document cannot support the pin capability.",
                nameof(capabilities));
        }

        return resolvedState;
    }
}
