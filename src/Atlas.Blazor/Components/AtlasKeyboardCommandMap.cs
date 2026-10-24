namespace Atlas.Blazor.Components;

/// <summary>
///     标识语义化工作区键盘命令。
/// </summary>
public enum AtlasKeyboardCommand
{
    /// <summary>
    ///     激活下一个文档标签页。
    /// </summary>
    NextTab = 0,
    /// <summary>
    ///     激活上一个文档标签页。
    /// </summary>
    PreviousTab = 1,
    /// <summary>
    ///     关闭活动文档。
    /// </summary>
    CloseActiveDocument = 2,
    /// <summary>
    ///     将焦点移向行内起始侧。
    /// </summary>
    FocusInlineStart = 3,
    /// <summary>
    ///     将焦点移向行内结束侧。
    /// </summary>
    FocusInlineEnd = 4,
    /// <summary>
    ///     将焦点移向块起始侧。
    /// </summary>
    FocusBlockStart = 5,
    /// <summary>
    ///     将焦点移向块结束侧。
    /// </summary>
    FocusBlockEnd = 6,
}

/// <summary>
///     使用 <c>KeyboardEvent.code</c> 描述物理按键组合。
/// </summary>
public sealed class AtlasKeyboardChord
{
    /// <summary>
    ///     初始化物理按键组合。
    /// </summary>
    public AtlasKeyboardChord(
        string code,
        bool control = false,
        bool shift = false,
        bool alt = false,
        bool meta = false)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A keyboard code is required.", nameof(code));
        }

        Code = code;
        Control = control;
        Shift = shift;
        Alt = alt;
        Meta = meta;
    }

    /// <summary>
    ///     获取物理 <c>KeyboardEvent.code</c>。
    /// </summary>
    public string Code { get; }
    /// <summary>
    ///     获取是否必须按下 Control。
    /// </summary>
    public bool Control { get; }
    /// <summary>
    ///     获取是否必须按下 Shift。
    /// </summary>
    public bool Shift { get; }
    /// <summary>
    ///     获取是否必须按下 Alt。
    /// </summary>
    public bool Alt { get; }
    /// <summary>
    ///     获取是否必须按下 Meta。
    /// </summary>
    public bool Meta { get; }
}

/// <summary>
///     把物理按键组合绑定到语义命令。
/// </summary>
public sealed class AtlasKeyboardBinding
{
    /// <summary>
    ///     初始化键盘绑定。
    /// </summary>
    public AtlasKeyboardBinding(
        AtlasKeyboardChord chord,
        AtlasKeyboardCommand command,
        bool isGlobal = false)
    {
        Chord = chord ?? throw new ArgumentNullException(nameof(chord));
        if (command is < AtlasKeyboardCommand.NextTab
            or > AtlasKeyboardCommand.FocusBlockEnd)
        {
            throw new ArgumentOutOfRangeException(nameof(command));
        }

        Command = command;
        IsGlobal = isGlobal;
    }

    /// <summary>
    ///     获取按键组合。
    /// </summary>
    public AtlasKeyboardChord Chord { get; }
    /// <summary>
    ///     获取语义命令。
    /// </summary>
    public AtlasKeyboardCommand Command { get; }
    /// <summary>
    ///     获取绑定是否在可编辑控件内也会触发。
    /// </summary>
    public bool IsGlobal { get; }
}

/// <summary>
///     为单个 Host 提供不可变键盘绑定集。
/// </summary>
public sealed class AtlasKeyboardCommandMap
{
    // 内置默认命令映射：常用的标签页切换、关闭与焦点移动组合键。
    // TypeScript 控制器保留一份镜像回退副本
    // （ClientScripts/controller.ts defaultKeyboardBindings）用于静态夹具挂载；
    // 两份列表必须保持同步。
    private static readonly AtlasKeyboardBinding[] DefaultBindings =
    {
        new(new AtlasKeyboardChord("Tab", control: true), AtlasKeyboardCommand.NextTab),
        new(
            new AtlasKeyboardChord("Tab", control: true, shift: true),
            AtlasKeyboardCommand.PreviousTab),
        new(
            new AtlasKeyboardChord("KeyW", control: true),
            AtlasKeyboardCommand.CloseActiveDocument),
        new(
            new AtlasKeyboardChord("F4", control: true),
            AtlasKeyboardCommand.CloseActiveDocument),
        new(
            new AtlasKeyboardChord("ArrowLeft", alt: true),
            AtlasKeyboardCommand.FocusInlineStart),
        new(
            new AtlasKeyboardChord("ArrowRight", alt: true),
            AtlasKeyboardCommand.FocusInlineEnd),
        new(
            new AtlasKeyboardChord("ArrowUp", alt: true),
            AtlasKeyboardCommand.FocusBlockStart),
        new(
            new AtlasKeyboardChord("ArrowDown", alt: true),
            AtlasKeyboardCommand.FocusBlockEnd),
    };

    /// <summary>
    ///     初始化键盘命令映射；同一按键组合（code 与修饰键）只能绑定一次，
    ///     重复项会抛出异常。
    /// </summary>
    public AtlasKeyboardCommandMap(IEnumerable<AtlasKeyboardBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        Bindings = Array.AsReadOnly(bindings.ToArray());
        if (Bindings.Any(binding => binding is null))
        {
            throw new ArgumentException("Bindings cannot contain null.", nameof(bindings));
        }

        if (Bindings
            .GroupBy(
                binding => (
                    binding.Chord.Code,
                    binding.Chord.Control,
                    binding.Chord.Shift,
                    binding.Chord.Alt,
                    binding.Chord.Meta))
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "A keyboard chord can only be bound once.",
                nameof(bindings));
        }
    }

    /// <summary>
    ///     获取默认 Atlas 命令映射。
    /// </summary>
    public static AtlasKeyboardCommandMap Default { get; } = new(DefaultBindings);

    /// <summary>
    ///     获取按匹配顺序排列的绑定。
    /// </summary>
    public IReadOnlyList<AtlasKeyboardBinding> Bindings { get; }
}
