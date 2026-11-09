using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace Allowance.Shell;

/// <summary>Draws the tray icon: session percent above weekly percent.</summary>
internal static class TrayNumberIcon
{
    public static Bitmap Render(int? session, int? weekly, int size, bool lightTaskbar)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        using var ink = new SolidBrush(lightTaskbar ? Color.FromArgb(28, 28, 28) : Color.White);
        var rowHeight = size / 2f;
        Draw(g, Text(session), new RectangleF(0, 0, size, rowHeight), ink);
        Draw(g, Text(weekly), new RectangleF(0, rowHeight, size, rowHeight), ink);
        return bitmap;
    }

    private static string Text(int? value) => value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-";

    private static void Draw(Graphics g, string text, RectangleF box, Brush ink)
    {
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.Alignment = StringAlignment.Center;
        format.LineAlignment = StringAlignment.Center;
        format.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.NoClip;

        // Largest size that fits the row; "100" needs a smaller one than "27".
        var em = box.Height * 1.25f;
        while (em > 5)
        {
            using var probe = new Font("Segoe UI Semibold", em, FontStyle.Regular, GraphicsUnit.Pixel);
            if (g.MeasureString(text, probe, PointF.Empty, format).Width <= box.Width)
            {
                break;
            }

            em -= 0.5f;
        }

        using var font = new Font("Segoe UI Semibold", em, FontStyle.Regular, GraphicsUnit.Pixel);
        g.DrawString(text, font, ink, box, format);
    }
}
