using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Jsonsui.Core.Models;

namespace Jsonsui.UI.Controls;

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
        // TEMP DIAG: prüfen, ob das Control erzeugt wird und das Theme gefunden wird.
        ResolveBaseTheme();

        // Retry, sobald das Control in den Visual Tree gehängt wird – zu diesem Zeitpunkt
        // sind die App-Ressourcen (FluentTheme) definitiv aufgelöst.
        AttachedToLogicalTree += (_, _) => ResolveBaseTheme();
    }

    private void ResolveBaseTheme()
    {
        if (Theme is not null) return;

        if (Application.Current is not { } app)
        {
            System.Diagnostics.Debug.WriteLine("[SchemaNumericUpDown] Application.Current is null (Theme bleibt ungesetzt).");
            return;
        }

        // Zuerst mit explizitem ThemeVariant (robust), dann ohne.
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

        System.Diagnostics.Debug.WriteLine("[SchemaNumericUpDown] WARN: kein NumericUpDown-ControlTheme via TryGetResource gefunden -> Control wäre unsichtbar.");
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
