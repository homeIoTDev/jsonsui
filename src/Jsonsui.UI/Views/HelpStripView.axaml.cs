using Avalonia;
using Avalonia.Controls;

namespace Jsonsui.UI.Views;

public partial class HelpStripView : UserControl
{
    public static readonly StyledProperty<bool> ShowMetaItemsProperty =
        AvaloniaProperty.Register<HelpStripView, bool>(nameof(ShowMetaItems));

    public static readonly StyledProperty<bool> ShowDetailsProperty =
        AvaloniaProperty.Register<HelpStripView, bool>(nameof(ShowDetails));

    public static readonly StyledProperty<bool> CompactProperty =
        AvaloniaProperty.Register<HelpStripView, bool>(nameof(Compact));

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<HelpStripView, double>(nameof(Spacing), 3.0);

    public HelpStripView()
    {
        InitializeComponent();
    }

    public bool ShowMetaItems
    {
        get => GetValue(ShowMetaItemsProperty);
        set => SetValue(ShowMetaItemsProperty, value);
    }

    public bool ShowDetails
    {
        get => GetValue(ShowDetailsProperty);
        set => SetValue(ShowDetailsProperty, value);
    }

    public bool Compact
    {
        get => GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }
}
