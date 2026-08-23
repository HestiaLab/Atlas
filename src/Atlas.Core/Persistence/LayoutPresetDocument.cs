namespace Atlas.Core.Persistence;

/// <summary>
///     不可变的命名预设载荷：描述符元数据与序列化 XML。
/// </summary>
public sealed class LayoutPresetDocument
{
    /// <summary>
    ///     构造预设文档；XML 必须非空。
    /// </summary>
    public LayoutPresetDocument(LayoutPresetDescriptor descriptor, string xml)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        Xml = IdentifierGuard.RequireValue(xml, nameof(xml));
    }

    /// <summary>
    ///     获取预设元数据。
    /// </summary>
    public LayoutPresetDescriptor Descriptor { get; }

    /// <summary>
    ///     获取持久化的 XML。
    /// </summary>
    public string Xml { get; }
}
