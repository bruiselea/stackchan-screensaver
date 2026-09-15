using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Stackchan {
    // Geometry and expression masks ported from macos-saver/StackchanSaverView.swift.
    public static class FaceRenderer {
        public static void Draw(Graphics g, Size size, Expression expression, bool eyesOpen, double time, double gazeX, double gazeY) {
            g.Clear(Color.Black);
            if (size.Width <= 0 || size.Height <= 0) return;
            GraphicsState saved = g.Save();
            try {
                float scale = Math.Min(size.Width / 320f, size.Height / 240f);
                g.TranslateTransform((size.Width - 320 * scale) / 2, (size.Height - 240 * scale) / 2);
                g.ScaleTransform(scale, scale);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                DrawEye(g, 90 + (float)gazeX * 3, 94 + (float)gazeY * 3, false, expression, eyesOpen);
                DrawEye(g, 230 + (float)gazeX * 3, 94 + (float)gazeY * 3, true, expression, eyesOpen);
                g.FillRectangle(Brushes.White, 115, 146 + (float)Math.Sin(time * 1.6) * 2, 90, 4);
            } finally { g.Restore(saved); }
        }
        private static void DrawEye(Graphics g, float x, float y, bool isLeft, Expression expression, bool open) {
            const float r = 8;
            if (!open) { g.FillRectangle(Brushes.White, x - r, y - 2, r * 2, 4); return; }
            g.FillEllipse(Brushes.White, x - r, y - r, r * 2, r * 2);
            if (expression == Expression.Angry || expression == Expression.Sad) {
                bool condition = (!isLeft) != !(expression == Expression.Sad);
                g.FillPolygon(Brushes.Black, new PointF[] {
                    new PointF(x - r, y - r), new PointF(x + r, y - r), new PointF(condition ? x - r : x + r, y)
                });
            }
            if (expression == Expression.Happy || expression == Expression.Sleepy) {
                float top = y - r;
                if (expression == Expression.Happy) {
                    top += r;
                    float inner = r / 1.5f;
                    g.FillEllipse(Brushes.Black, x - inner, y - inner, inner * 2, inner * 2);
                }
                g.FillRectangle(Brushes.Black, x - r, top, r * 2 + 4, r + 2);
            }
        }
    }
}
