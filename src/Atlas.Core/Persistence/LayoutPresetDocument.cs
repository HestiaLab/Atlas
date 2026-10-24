namespace Atlas.Core.Persistence;

/// <summary>
///     不可变的命名预设负载：描述符元数据与序列化 XML。
/// </summary>
public sealed class LayoutPresetDocument
{
    /// <summary>
    ///     创建预设文档；XML 不得为空。
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
