using Allowance.Usage;
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
        AutomationProperties.SetName(Bar, $"{row.Label}: {row.Percent}% used");
    }
}
