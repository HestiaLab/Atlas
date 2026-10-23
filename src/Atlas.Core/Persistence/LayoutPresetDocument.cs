namespace Atlas.Core.Persistence;

/// <summary>
///     Immutable named preset payload: descriptor metadata and serialized XML.
/// </summary>
public sealed class LayoutPresetDocument
{
    /// <summary>
    ///     Creates a preset document; the XML must be non-empty.
    /// </summary>
    public LayoutPresetDocument(LayoutPresetDescriptor descriptor, string xml)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        Xml = IdentifierGuard.RequireValue(xml, nameof(xml));
    }

    /// <summary>
    ///     Gets the preset metadata.
    /// </summary>
    public LayoutPresetDescriptor Descriptor { get; }

    /// <summary>
    ///     Gets the persisted XML.
    /// </summary>
    public string Xml { get; }
}
