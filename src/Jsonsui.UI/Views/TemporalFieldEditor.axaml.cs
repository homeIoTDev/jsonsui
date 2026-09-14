using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Jsonsui.Core.Models;
using Jsonsui.Core.Services;
using Jsonsui.UI.ViewModels;

namespace Jsonsui.UI.Views;

public partial class TemporalFieldEditor : UserControl
{
    private Flyout? _calendarFlyout;
    private Flyout? _timeFlyout;

    public TemporalFieldEditor()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? Vm
        => TopLevel.GetTopLevel(this)?.DataContext as MainWindowViewModel;

    private static FieldRow? RowOf(Control c) => c.DataContext as FieldRow;

    private void TemporalText_GotFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && RowOf(tb) is { } row && Vm is { } vm)
            vm.FocusFieldCommand.Execute(row.Path);
    }

    private void TemporalText_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && RowOf(tb) is { } row)
            CommitText(tb, row);
    }

    private void TemporalText_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb && RowOf(tb) is { } row)
            CommitText(tb, row);
    }

    private void CommitText(TextBox tb, FieldRow row)
    {
        if (Vm is not { } vm) return;

        var canonical = TemporalValue.Normalize(row.FieldType, tb.Text);
        if (canonical == null)
        {
            tb.Text = TemporalValue.Format(row, row.FieldType) ?? "";
            return;
        }

        if (canonical == vm.GetFieldRawValue(row.Path)) return;

        TemporalValue.Apply(row, row.FieldType, canonical);
        vm.ApplyTemporalChange(row.Path, canonical);
        tb.Text = canonical;
    }

    private void CalendarFlyout_Opened(object? sender, EventArgs e)
    {
        if (sender is not Flyout f || f.Content is not Calendar cal) return;
        _calendarFlyout = f;
        if (f.Target?.DataContext is not FieldRow row) return;
        cal.DisplayDate = row.DateValue?.Date ?? DateTime.Today;
        cal.SelectedDate = row.DateValue?.Date;
    }

    private void Calendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not Calendar cal || cal.DataContext is not FieldRow row) return;
        if (Vm is not { } vm) return;
        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not DateTime added) return;

        var newDate = new DateTimeOffset(added.Year, added.Month, added.Day, 0, 0, 0, TimeSpan.Zero);

        if (row.DateValue.HasValue &&
            row.DateValue.Value.Year == newDate.Year &&
            row.DateValue.Value.Month == newDate.Month &&
            row.DateValue.Value.Day == newDate.Day)
            return;

        var canonical = row.FieldType == "date-time"
            ? TemporalValue.FormatDateTime(newDate, row.TimeValue, row.UseSeconds, row.HasOffset, row.IsUtcSuffix, row.Offset)
            : TemporalValue.FormatDate(newDate);

        if (canonical == null) return;

        TemporalValue.Apply(row, row.FieldType, canonical);
        vm.ApplyTemporalChange(row.Path, canonical);
        TemporalTextBox.Text = canonical;
        _calendarFlyout?.Hide();
    }

    private void TimeFlyout_Opened(object? sender, EventArgs e)
    {
        if (sender is not Flyout f || f.Content is not TimePicker tp) return;
        _timeFlyout = f;
        if (f.Target?.DataContext is not FieldRow row) return;
        tp.UseSeconds = row.UseSeconds;
        tp.SelectedTime = row.TimeValue;
    }

    private void TimeFlyoutPicker_SelectedTimeChanged(object? sender, TimePickerSelectedValueChangedEventArgs e)
    {
        if (sender is not TimePicker tp || tp.DataContext is not FieldRow row) return;
        if (Vm is not { } vm) return;

        var newTime = e.NewTime;
        if (newTime == null && row.TimeValue == null) return;
        if (newTime.HasValue && row.TimeValue.HasValue && newTime.Value == row.TimeValue.Value) return;

        var canonical = row.FieldType == "date-time"
            ? TemporalValue.FormatDateTime(row.DateValue, newTime, row.UseSeconds, row.HasOffset, row.IsUtcSuffix, row.Offset)
            : TemporalValue.FormatTime(newTime, row.UseSeconds);

        if (canonical == null) return;

        TemporalValue.Apply(row, row.FieldType, canonical);
        vm.ApplyTemporalChange(row.Path, canonical);
        TemporalTextBox.Text = canonical;
        _timeFlyout?.Hide();
    }
}
