using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SoundSwitch
{
    internal static class LoopIcon
    {
        internal static Bitmap Draw(int arrows, Color color, int size)
        {
            if (arrows < 0) throw new ArgumentOutOfRangeException("arrows");
            if (size < 16 || size > 256) throw new ArgumentOutOfRangeException("size");

            var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var brush = new SolidBrush(color))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float center = size / 2f;
                float radius = size * 0.30f;
                float width = Math.Min(size * 0.105f, size * 0.5f / Math.Max(1, arrows));
                using (var pen = new Pen(color, width))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    if (arrows == 0)
                    {
                        graphics.DrawEllipse(pen, center - radius, center - radius, radius * 2, radius * 2);
                    }
                    else
                    {
                        // shortcut: arrows get dense above six outputs, enlarge the icon if larger counts become common.
                        float segment = 360f / arrows;
                        float head = Math.Min(size * 0.22f, radius * segment * (float)Math.PI / 180f * 0.36f);
                        float gap = Math.Min(28f, segment * 0.23f);
                        for (int index = 0; index < arrows; index++)
                        {
                            float start = -90f + index * segment;
                            float end = start + segment - gap;
                            graphics.DrawArc(pen, center - radius, center - radius, radius * 2, radius * 2,
                                start, segment - gap - head / radius * 36f);
                            double angle = end * Math.PI / 180;
                            float x = (float)Math.Cos(angle), y = (float)Math.Sin(angle);
                            var tip = new PointF(center + radius * x, center + radius * y);
                            var middle = new PointF(tip.X + y * head, tip.Y - x * head);
                            graphics.FillPolygon(brush, new[]
                            {
                                tip,
                                new PointF(middle.X + x * head * 0.55f, middle.Y + y * head * 0.55f),
                                new PointF(middle.X - x * head * 0.55f, middle.Y - y * head * 0.55f)
                            });
                        }
                    }
                }
            }
            return bitmap;
        }

        internal static Icon Create(int arrows, Color color, int size)
        {
            using (Bitmap bitmap = Draw(arrows, color, size))
            {
                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon borrowed = Icon.FromHandle(handle)) return (Icon)borrowed.Clone();
                }
                finally { DestroyIcon(handle); }
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr icon);
    }
}
