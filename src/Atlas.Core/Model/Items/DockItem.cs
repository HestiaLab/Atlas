namespace Atlas.Core.Items;

/// <summary>
///     An immutable tool item or document item in a workspace;
///     the constructor validates the combination of kind, capability set, and document state, after which
///     all properties are read-only.
/// </summary>
public sealed class DockItem
{
    /// <summary>
    ///     Constructs an immutable item and validates the combination constraints: Id and Content must not
    ///     be default values and kind is limited to tool/document; Capabilities must be a subset allowed for
    ///     that kind (tool items cannot be split or pinned, document items cannot be auto-hidden); tool items
    ///     must not carry DocumentState, a pinned document must declare CanPinDocument, and a preview document
    ///     must not.
    /// </summary>
    /// <param name="id">The item identifier within the workspace scope.</param>
    /// <param name="kind">The behavioural kind of the item.</param>
    /// <param name="content">The content renderer and resource identifier.</param>
    /// <param name="capabilities">The stable capability set of the item; must match the kind.</param>
    /// <param name="title">The display title; when <see langword="null"/> the item identifier is used.</param>
    /// <param name="iconKey">The optional UI-agnostic icon identifier.</param>
    /// <param name="documentState">State committed by documents only; a tool item throws when a non-<see langword="null"/> value is passed.</param>
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
    ///     The item identifier within the workspace scope.
    /// </summary>
    public DockItemId Id { get; }

    /// <summary>
    ///     The behavioural kind of the item: determines the hosting group and the allowed capability set.
    /// </summary>
    public DockItemKind Kind { get; }

    /// <summary>
    ///     The display title; falls back to the string value of <see cref="Id"/> when not supplied at construction.
    /// </summary>
    public string Title { get; }

    /// <summary>
    ///     The optional UI-agnostic icon identifier; <see langword="null"/> means no icon.
    /// </summary>
    public string? IconKey { get; }

    /// <summary>
    ///     The content reference: renderer kind and resource identifier, also the basis for content identity comparison and deduplication decisions.
    /// </summary>
    public ContentReference Content { get; }

    /// <summary>
    ///     The stable capability set of the item: declares the user operations the item supports and is validated against the kind at construction.
    /// </summary>
    public DockItemCapabilities Capabilities { get; }

    /// <summary>
    ///     The pinned/preview state of document items; always <see langword="null"/> for tool items.
    /// </summary>
    public DocumentState? DocumentState { get; }

    private static void ValidateCapabilities(
        DockItemKind kind,
        DockItemCapabilities capabilities)
    {
        if ((capabilities & ~DockItemCapabilityPresets.KnownCapabilities) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capabilities),
                capabilities,
                "The capability set contains unknown flags.");
        }

        if ((capabilities & ~DockItemCapabilityPresets.AllowedFor(kind)) != 0)
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
