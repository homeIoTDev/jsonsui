using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using ConfixJson.Core.Models;

namespace ConfixJson.UI.Controls;

/// <summary>
/// NumericUpDown, dessen Spin-Buttons/Pfeiltasten auf die Schema-Grenzen
/// (FieldRow.MinValue/MaxValue) geklemmt werden. Minimum/Maximum bleiben
/// ungebunden, damit die manuelle Texteingabe auch außerhalb der Grenzen
/// möglich ist und erst die JSON-Schema-Validierung den Fehler erzeugt.
/// </summary>
public class SchemaNumericUpDown : NumericUpDown
{
    public SchemaNumericUpDown()
    {
        // Avalonia resolves the default ControlTheme by the exact control type, so a
        // subclass gets no theme/template (=> invisible). Inherit the base theme.
        if (Application.Current is { } app &&
            app.TryGetResource(typeof(NumericUpDown), out var theme) &&
            theme is ControlTheme controlTheme)
        {
            Theme = controlTheme;
        }
    }

    protected override void OnSpin(SpinEventArgs e)
    {
        Step(e.Direction == SpinDirection.Increase ? +Increment : -Increment);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                Step(+Increment);
                e.Handled = true;
                break;
            case Key.Down:
                Step(-Increment);
                e.Handled = true;
                break;
            default:
                base.OnKeyDown(e);
                break;
        }
    }

    private void Step(decimal delta)
    {
        if (DataContext is not FieldRow row)
        {
            Value = (Value ?? 0m) + delta;
            return;
        }

        var current = Value ?? 0m;
        var next = Math.Clamp(current + delta, row.MinValue, row.MaxValue);
        if (next != current)
            Value = next;
    }
}
