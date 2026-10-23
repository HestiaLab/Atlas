namespace Atlas.Blazor.Components;

/// <summary>
///     Identifies semantic workspace keyboard commands.
/// </summary>
public enum AtlasKeyboardCommand
{
    /// <summary>
    ///     Activates the next document tab.
    /// </summary>
    NextTab = 0,
    /// <summary>
    ///     Activates the previous document tab.
    /// </summary>
    PreviousTab = 1,
    /// <summary>
    ///     Closes the active document.
    /// </summary>
    CloseActiveDocument = 2,
    /// <summary>
    ///     Moves focus toward inline-start.
    /// </summary>
    FocusInlineStart = 3,
    /// <summary>
    ///     Moves focus toward inline-end.
    /// </summary>
    FocusInlineEnd = 4,
    /// <summary>
    ///     Moves focus toward block-start.
    /// </summary>
    FocusBlockStart = 5,
    /// <summary>
    ///     Moves focus toward block-end.
    /// </summary>
    FocusBlockEnd = 6,
}

/// <summary>
///     Describes a physical key combination using <c>KeyboardEvent.code</c>.
/// </summary>
public sealed class AtlasKeyboardChord
{
    /// <summary>
    ///     Initializes a physical key combination.
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
    ///     Gets the physical <c>KeyboardEvent.code</c>.
    /// </summary>
    public string Code { get; }
    /// <summary>
    ///     Gets whether Control must be pressed.
    /// </summary>
    public bool Control { get; }
    /// <summary>
    ///     Gets whether Shift must be pressed.
    /// </summary>
    public bool Shift { get; }
    /// <summary>
    ///     Gets whether Alt must be pressed.
    /// </summary>
    public bool Alt { get; }
    /// <summary>
    ///     Gets whether Meta must be pressed.
    /// </summary>
    public bool Meta { get; }
}

/// <summary>
///     Binds a physical key combination to a semantic command.
/// </summary>
public sealed class AtlasKeyboardBinding
{
    /// <summary>
    ///     Initializes a keyboard binding.
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
    ///     Gets the key combination.
    /// </summary>
    public AtlasKeyboardChord Chord { get; }
    /// <summary>
    ///     Gets the semantic command.
    /// </summary>
    public AtlasKeyboardCommand Command { get; }
    /// <summary>
    ///     Gets whether the binding also fires inside editable controls.
    /// </summary>
    public bool IsGlobal { get; }
}

/// <summary>
///     Provides an immutable keyboard binding set for a single Host.
/// </summary>
public sealed class AtlasKeyboardCommandMap
{
    // Built-in default command map: the common tab switch, close, and focus
    // movement chords. The TypeScript controller keeps a mirrored fallback
    // copy (ClientScripts/controller.ts defaultKeyboardBindings) for
    // static-fixture attachments; the two lists must stay in sync.
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
    ///     Initializes a keyboard command map; the same key combination (code
    ///     and modifiers) can only be bound once, and duplicates throw.
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
    ///     Gets the default Atlas command map.
    /// </summary>
    public static AtlasKeyboardCommandMap Default { get; } = new(DefaultBindings);

    /// <summary>
    ///     Gets the bindings in matching order.
    /// </summary>
    public IReadOnlyList<AtlasKeyboardBinding> Bindings { get; }
}
