using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Jsonsui.Core.Models;

namespace Jsonsui.UI.Controls;

/// <summary>
/// NumericUpDown whose spin buttons/arrow keys are clamped to the schema bounds
/// (FieldRow.MinValue/MaxValue). Minimum/Maximum stay unbound so manual text input
/// is still possible outside the bounds and only JSON schema validation reports the error.
/// </summary>
public class SchemaNumericUpDown : NumericUpDown
{
    public SchemaNumericUpDown()
    {
        // TEMP DIAG: verify that the control is created and the theme is found.
        ResolveBaseTheme();

        // Retry once the control is attached to the logical tree - at that point the
        // app resources (FluentTheme) are definitely resolved.
        AttachedToLogicalTree += (_, _) => ResolveBaseTheme();
    }

    private void ResolveBaseTheme()
    {
        if (Theme is not null) return;

        if (Application.Current is not { } app)
        {
            System.Diagnostics.Debug.WriteLine("[SchemaNumericUpDown] Application.Current is null (theme stays unset).");
            return;
        }

        // First with an explicit ThemeVariant (robust), then without.
        if (app.TryGetResource(typeof(NumericUpDown), Avalonia.Styling.ThemeVariant.Default, out var v1) && v1 is ControlTheme t1)
        {
            Theme = t1;
            System.Diagnostics.Debug.WriteLine($"[SchemaNumericUpDown] Theme inherited (variant). type={t1.GetType().Name}");
            return;
        }

        if (app.TryGetResource(typeof(NumericUpDown), out var v2) && v2 is ControlTheme t2)
        {
            Theme = t2;
            System.Diagnostics.Debug.WriteLine($"[SchemaNumericUpDown] Theme inherited. type={t2.GetType().Name}");
            return;
        }

        System.Diagnostics.Debug.WriteLine("[SchemaNumericUpDown] WARN: no NumericUpDown ControlTheme found via TryGetResource -> control would be invisible.");
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
