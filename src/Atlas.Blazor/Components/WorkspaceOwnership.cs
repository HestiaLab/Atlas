namespace Atlas.Blazor.Components;

/// <summary>
///     控制编程式提供的工作区的所有权。
/// </summary>
public enum WorkspaceOwnership
{
    /// <summary>
    ///     声明式模式使用宿主所有权，编程式模式使用外部所有权。
    /// </summary>
    Automatic = 0,

    /// <summary>
    ///     工作区由应用程序拥有并释放。
    /// </summary>
    External = 1,

    /// <summary>
    ///     工作区由宿主拥有并释放。
    /// </summary>
    Host = 2,
}
