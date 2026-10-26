using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace PowerUI
{
    internal enum TrayDevice { Mouse, Keyboard, Headphones, Speaker }

    internal static class TrayIcon
    {
        internal static Icon Create(int size, int percent, bool dark, TrayDevice device)
        {
            using (var bitmap = Render(size, percent, dark, device))
            {
                IntPtr native = bitmap.GetHicon();
                try { return (Icon)Icon.FromHandle(native).Clone(); }
                finally { DesktopNative.DestroyIcon(native); }
            }
        }

        internal static Bitmap Render(int size, int percent, bool dark, TrayDevice device)
        {
            var bitmap = new Bitmap(size, size);
            using (var graphics = Graphics.FromImage(bitmap))
            using (var track = new Pen(dark ? Color.FromArgb(89, 93, 97) : Color.FromArgb(155, 159, 163), 3.6f))
            using (var charge = new Pen(percent <= 20 ? Color.FromArgb(255, 65, 69)
                : percent <= 40 ? Color.FromArgb(255, 205, 32) : Color.FromArgb(48, 224, 65), 3.6f))
            using (var ink = new SolidBrush(dark ? Color.White : Color.FromArgb(24, 26, 28)))
            using (var stroke = new Pen(ink, 1.4f))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.ScaleTransform(size / 32f, size / 32f);
                if (percent < 0) track.DashStyle = DashStyle.Dot;
                graphics.DrawEllipse(track, 2, 2, 28, 28);
                charge.StartCap = charge.EndCap = LineCap.Round;
                if (percent >= 100) graphics.DrawEllipse(charge, 2, 2, 28, 28);
                else if (percent > 0) graphics.DrawArc(charge, 2, 2, 28, 28, -90, 360f * percent / 100);

                switch (device)
                {
                    case TrayDevice.Mouse:
                        using (var body = new GraphicsPath())
                        {
                            body.AddArc(11, 7, 10, 10, 180, 180);
                            body.AddArc(11, 15, 10, 10, 0, 180);
                            body.CloseFigure();
                            graphics.FillPath(ink, body);
                        }
                        // Clear the wheel instead of painting a taskbar-colored patch.
                        graphics.CompositingMode = CompositingMode.SourceCopy;
                        using (var clear = new Pen(Color.Transparent, 1.6f))
                        {
                            clear.StartCap = clear.EndCap = LineCap.Round;
                            graphics.DrawLine(clear, 16, 10, 16, 13);
                        }
                        break;
                    case TrayDevice.Keyboard:
                        graphics.DrawRectangle(stroke, 7.5f, 11, 17, 10);
                        for (int row = 0; row < 2; row++)
                            for (int col = 0; col < 4; col++)
                                graphics.FillRectangle(ink, 10 + col * 3.3f, 13 + row * 2.5f, 1.8f, 1.3f);
                        graphics.FillRectangle(ink, 12, 18, 8, 1.3f);
                        break;
                    case TrayDevice.Headphones:
                        stroke.Width = 2;
                        graphics.DrawArc(stroke, 9, 8, 14, 17, 180, 180);
                        graphics.FillRectangle(ink, 8, 16, 4, 8);
                        graphics.FillRectangle(ink, 20, 16, 4, 8);
                        break;
                    case TrayDevice.Speaker:
                        graphics.DrawRectangle(stroke, 10.5f, 7.5f, 11, 17);
                        graphics.FillEllipse(ink, 14, 10, 4, 4);
                        graphics.DrawEllipse(stroke, 13, 16, 6, 6);
                        break;
                }
            }
            return bitmap;
        }
    }
}
