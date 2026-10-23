using System.Xml;

namespace Atlas.Core.Persistence;

/// <summary>
///     标识持久化文档超过资源限制；用于把限制失败与一般 XML/schema 失败区分开。
/// </summary>
internal sealed class PersistenceLimitException : XmlException
{
    internal PersistenceLimitException(string message)
        : base(message)
    {
    }
}
