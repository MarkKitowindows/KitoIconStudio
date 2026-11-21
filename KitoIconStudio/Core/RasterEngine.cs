using System.Windows.Media;

namespace KitoIconStudio.Core;

public static class RasterEngine
{
    public static int ToPixel(Color color) => (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;

    public static Color ToColor(int color) => Color.FromArgb(
        (byte)((uint)color >> 24), (byte)((color >> 16) & 255),
        (byte)((color >> 8) & 255), (byte)(color & 255));

    public static int[] Composite(IconDocument document)
    {
        var result = new int[document.Width * document.Height];
        for (int layerIndex = document.Layers.Count - 1; layerIndex >= 0; layerIndex--)
        {
            var layer = document.Layers[layerIndex];
            if (!layer.IsVisible) continue;
            for (int i = 0; i < result.Length; i++)
                result[i] = Blend(result[i], layer.Pixels[i]);
        }
        return result;
    }

    // Standard source-over alpha compositing with straight (unpremultiplied) RGB.
    public static int Blend(int destination, int source)
    {
        int sourceAlpha = (int)((uint)source >> 24);
        if (sourceAlpha == 0) return destination;
        if (sourceAlpha == 255) return source;
        int dstAlpha = (int)((uint)destination >> 24);
        double s = sourceAlpha / 255.0;
        double d = (dstAlpha / 255.0) * (1.0 - s);
        double outA = s + d;
        if (outA <= 0) return 0;
        int a = (int)Math.Round(outA * 255);
        int r = (int)Math.Round((((source >> 16) & 255) * s + ((destination >> 16) & 255) * d) / outA);
        int g = (int)Math.Round((((source >> 8) & 255) * s + ((destination >> 8) & 255) * d) / outA);
        int b = (int)Math.Round(((source & 255) * s + (destination & 255) * d) / outA);
        return ((a & 255) << 24) | ((r & 255) << 16) | ((g & 255) << 8) | (b & 255);
    }

    public static void Stamp(int[] pixels, int width, int x, int y, int radius, int color)
    {
        int height = pixels.Length / width;
        radius = Math.Max(0, radius);
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            if (dx * dx + dy * dy > radius * radius + radius) continue;
            int px = x + dx, py = y + dy;
            if (px < 0 || py < 0 || px >= width || py >= height) continue;
            pixels[py * width + px] = color;
        }
    }

    public static void Line(int[] pixels, int width, int x0, int y0, int x1, int y1, int radius, int color)
    {
        int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int error = dx - dy;
        while (true)
        {
            Stamp(pixels, width, x0, y0, radius, color);
            if (x0 == x1 && y0 == y1) break;
            int twice = error * 2;
            if (twice > -dy) { error -= dy; x0 += sx; }
            if (twice < dx) { error += dx; y0 += sy; }
        }
    }

    public static void Rectangle(int[] pixels, int width, int x0, int y0, int x1, int y1, int radius, int color)
    {
        int left = Math.Min(x0, x1), right = Math.Max(x0, x1);
        int top = Math.Min(y0, y1), bottom = Math.Max(y0, y1);
        Line(pixels, width, left, top, right, top, radius, color);
        Line(pixels, width, right, top, right, bottom, radius, color);
        Line(pixels, width, right, bottom, left, bottom, radius, color);
        Line(pixels, width, left, bottom, left, top, radius, color);
    }

    public static void Ellipse(int[] pixels, int width, int x0, int y0, int x1, int y1, int radius, int color)
    {
        double rx = Math.Abs(x1 - x0) / 2.0;
        double ry = Math.Abs(y1 - y0) / 2.0;
        double cx = (x0 + x1) / 2.0;
        double cy = (y0 + y1) / 2.0;
        if (rx < 0.5 || ry < 0.5)
        {
            Line(pixels, width, x0, y0, x1, y1, radius, color);
            return;
        }
        int steps = Math.Max(36, (int)(2 * Math.PI * Math.Max(rx, ry) * 2));
        int lastX = (int)Math.Round(cx + rx), lastY = (int)Math.Round(cy);
        for (int s = 1; s <= steps; s++)
        {
            double angle = s * (2 * Math.PI / steps);
            int px = (int)Math.Round(cx + rx * Math.Cos(angle));
            int py = (int)Math.Round(cy + ry * Math.Sin(angle));
            Line(pixels, width, lastX, lastY, px, py, radius, color);
            lastX = px; lastY = py;
        }
    }

    public static void FloodFill(int[] pixels, int width, int x, int y, int replacement)
    {
        int height = pixels.Length / width;
        if (x < 0 || y < 0 || x >= width || y >= height) return;
        int start = y * width + x, target = pixels[start];
        if (target == replacement) return;
        var queue = new Queue<int>();
        queue.Enqueue(start);
        pixels[start] = replacement;
        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int px = i % width, py = i / width;
            void Enqueue(int p)
            {
                if (pixels[p] != target) return;
                pixels[p] = replacement;
                queue.Enqueue(p);
            }
            if (px > 0) Enqueue(i - 1);
            if (px < width - 1) Enqueue(i + 1);
            if (py > 0) Enqueue(i - width);
            if (py < height - 1) Enqueue(i + width);
        }
    }
}
