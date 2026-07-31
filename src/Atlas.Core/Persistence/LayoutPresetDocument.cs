namespace Atlas.Core.Persistence;

/// <summary>Contains immutable preset metadata and its persisted XML.</summary>
public sealed class LayoutPresetDocument
{
    /// <summary>Initializes a stored preset document.</summary>
    public LayoutPresetDocument(LayoutPresetDescriptor descriptor, string xml)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        Xml = IdentifierGuard.RequireValue(xml, nameof(xml));
    }

    /// <summary>Gets preset metadata.</summary>
    public LayoutPresetDescriptor Descriptor { get; }

    /// <summary>Gets the persisted XML.</summary>
    public string Xml { get; }
}
