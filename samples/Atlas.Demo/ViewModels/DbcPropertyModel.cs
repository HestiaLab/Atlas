using System.Globalization;
using Atlas.Demo.Models.Dbc;
using Atlas.Demo.Services;

namespace Atlas.Demo.ViewModels;

/// <summary>
/// PanelModel for the DBC Properties panel. Holds editable property values
/// for the selected file, message, or signal. <see cref="Apply"/> is the
/// save hook the panel calls after the user edits a field.
/// </summary>
public sealed class DbcPropertyModel
{
    private DbcMessage? _message;
    private DbcSignal? _signal;

    public string? TargetName { get; private set; }

    public string? TargetType { get; private set; }

    public string? Id { get; private set; }

    public string? Name { get; private set; }

    public string? Length { get; private set; }

    public string? Transmitter { get; private set; }

    public string? StartBit { get; private set; }

    public string? Factor { get; private set; }

    public string? Offset { get; private set; }

    public string? Min { get; private set; }

    public string? Max { get; private set; }

    public event Action? StateChanged;

    public void ShowFile(DbcFileStore.DbcFile file)
    {
        Reset();
        TargetType = "File";
        TargetName = file.Name;
        Name = file.Name;
        Notify();
    }

    public void ShowMessage(DbcMessage msg)
    {
        Reset();
        _message = msg;
        TargetType = "Message";
        TargetName = msg.Name;
        Id = msg.Id.ToString(CultureInfo.InvariantCulture);
        Name = msg.Name;
        Length = msg.Length.ToString(CultureInfo.InvariantCulture);
        Transmitter = msg.Transmitter;
        Notify();
    }

    public void ShowSignal(DbcSignal sig)
    {
        Reset();
        _signal = sig;
        TargetType = "Signal";
        TargetName = sig.Name;
        Name = sig.Name;
        StartBit = sig.StartBit.ToString(CultureInfo.InvariantCulture);
        Length = sig.Length.ToString(CultureInfo.InvariantCulture);
        Factor = FormatNumber(sig.Factor);
        Offset = FormatNumber(sig.Offset);
        Min = FormatNumber(sig.Min);
        Max = FormatNumber(sig.Max);
        Notify();
    }

    public void Apply()
    {
        Notify();
    }

    private void Reset()
    {
        _message = null;
        _signal = null;
        TargetName = null;
        TargetType = null;
        Id = null;
        Name = null;
        Length = null;
        Transmitter = null;
        StartBit = null;
        Factor = null;
        Offset = null;
        Min = null;
        Max = null;
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private void Notify()
    {
        StateChanged?.Invoke();
    }
}
