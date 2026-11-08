using System;
using System.Windows;
using System.Windows.Media;

namespace Pomodoro
{
    public static class Ring
    {
        public static Geometry Build(double size, double radius, double fraction)
        {
            if (fraction <= 0.0008) return null;
            double cx = size / 2.0;
            double cy = size / 2.0;

            if (fraction >= 0.9992)
                return new EllipseGeometry(new Point(cx, cy), radius, radius);

            double sweep = 360.0 * fraction;
            Point start = PointAt(cx, cy, radius, -90.0);
            Point end = PointAt(cx, cy, radius, -90.0 + sweep);

            var geo = new StreamGeometry();
            using (StreamGeometryContext ctx = geo.Open())
            {
                ctx.BeginFigure(start, false, false);
                ctx.ArcTo(end, new Size(radius, radius), 0.0, sweep > 180.0,
                          SweepDirection.Clockwise, true, false);
            }
            geo.Freeze();
            return geo;
        }

        private static Point PointAt(double cx, double cy, double r, double degrees)
        {
            double rad = degrees * Math.PI / 180.0;
            return new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
        }
    }
}
