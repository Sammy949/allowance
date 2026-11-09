using Allowance.Usage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Allowance.Drawer;

public sealed partial class UsageRowView : UserControl
{
    public UsageRowView() => InitializeComponent();

    internal void Show(UsageRow row, DateTimeOffset now)
    {
        LabelText.Text = row.Label;
        ResetText.Text = UsageText.Reset(row.ResetsAt, now, TimeZoneInfo.Local);
        PercentText.Text = $"{row.Percent}% used";
        Bar.Value = row.Percent;
        var elapsed = UsagePace.Elapsed(row, now);
        PaceLayer.Visibility = elapsed is null ? Visibility.Collapsed : Visibility.Visible;
        var name = $"{row.Label}: {row.Percent}% used";
        if (elapsed is { } value)
        {
            PaceDone.Width = new GridLength(value, GridUnitType.Star);
            PaceLeft.Width = new GridLength(1 - value, GridUnitType.Star);
            var passed = $"{Math.Round(value * 100)}% of this window has passed";
            ToolTipService.SetToolTip(BarArea, passed);
            name += $", {passed}";
        }

        AutomationProperties.SetName(Bar, name);
    }
}
