namespace Atlas.Blazor.Components;

/// <summary>Identifies a semantic workspace keyboard command.</summary>
public enum AtlasKeyboardCommand
{
    /// <summary>Activates the next document tab.</summary>
    NextTab = 0,
    /// <summary>Activates the previous document tab.</summary>
    PreviousTab = 1,
    /// <summary>Closes the active document.</summary>
    CloseActiveDocument = 2,
    /// <summary>Moves focus toward inline-start.</summary>
    FocusInlineStart = 3,
    /// <summary>Moves focus toward inline-end.</summary>
    FocusInlineEnd = 4,
    /// <summary>Moves focus toward block-start.</summary>
    FocusBlockStart = 5,
    /// <summary>Moves focus toward block-end.</summary>
    FocusBlockEnd = 6,
}

/// <summary>Describes one physical-key chord using <c>KeyboardEvent.code</c>.</summary>
public sealed class AtlasKeyboardChord
{
    /// <summary>Initializes a physical-key chord.</summary>
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

    /// <summary>Gets the physical <c>KeyboardEvent.code</c>.</summary>
    public string Code { get; }
    /// <summary>Gets whether Control is required.</summary>
    public bool Control { get; }
    /// <summary>Gets whether Shift is required.</summary>
    public bool Shift { get; }
    /// <summary>Gets whether Alt is required.</summary>
    public bool Alt { get; }
    /// <summary>Gets whether Meta is required.</summary>
    public bool Meta { get; }
}

/// <summary>Binds a physical-key chord to a semantic command.</summary>
public sealed class AtlasKeyboardBinding
{
    /// <summary>Initializes a keyboard binding.</summary>
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

    /// <summary>Gets the chord.</summary>
    public AtlasKeyboardChord Chord { get; }
    /// <summary>Gets the semantic command.</summary>
    public AtlasKeyboardCommand Command { get; }
    /// <summary>Gets whether the binding applies inside editable controls.</summary>
    public bool IsGlobal { get; }
}

/// <summary>Provides the immutable keyboard bindings for one Host.</summary>
public sealed class AtlasKeyboardCommandMap
{
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

    /// <summary>Initializes a keyboard command map.</summary>
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

    /// <summary>Gets the default Atlas command map.</summary>
    public static AtlasKeyboardCommandMap Default { get; } = new(DefaultBindings);

    /// <summary>Gets the bindings in matching order.</summary>
    public IReadOnlyList<AtlasKeyboardBinding> Bindings { get; }
}
