using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KitoIconStudio.Core;

public static class ExportService
{
    private static BitmapSource BitmapFromPixels(IconDocument document)
    {
        int[] pixels = RasterEngine.Composite(document);
        var bmp = BitmapSource.Create(document.Width, document.Height, 96, 96,
            PixelFormats.Bgra32, null, pixels, document.Width * 4);
        bmp.Freeze();
        return bmp;
    }

    public static void ExportPng(IconDocument document, string path)
    {
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(BitmapFromPixels(document)));
        using var stream = File.Create(path);
        png.Save(stream);
    }

    private static byte[] ToPngBytes(BitmapSource image, int size)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            // Each ICO entry is resampled to its specified pixel size.
            context.DrawImage(image, new Rect(0, 0, size, size));
        }
        var rendered = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(visual);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(rendered));
        using var stream = new MemoryStream();
        png.Save(stream);
        return stream.ToArray();
    }

    public static void ExportIco(IconDocument document, string path)
    {
        // ICO directory with embedded PNG images, supported by modern Windows.
        int[] sizes = [16, 32, 48, 256];
        BitmapSource source = BitmapFromPixels(document);
        byte[][] images = sizes.Select(size => ToPngBytes(source, size)).ToArray();
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0); // reserved
        writer.Write((ushort)1); // icon
        writer.Write((ushort)images.Length);
        int position = 6 + 16 * images.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
            writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
            writer.Write((byte)0); // palette colors
            writer.Write((byte)0); // reserved
            writer.Write((ushort)1); // color planes
            writer.Write((ushort)32); // bits per pixel
            writer.Write((uint)images[i].Length);
            writer.Write((uint)position);
            position += images[i].Length;
        }
        foreach (byte[] png in images)
            writer.Write(png);
    }

    public static int[] ImportImage(string path, int size)
    {
        var loaded = new BitmapImage();
        loaded.BeginInit();
        loaded.CacheOption = BitmapCacheOption.OnLoad;
        loaded.UriSource = new Uri(Path.GetFullPath(path));
        loaded.EndInit();
        loaded.Freeze();

        double factor = Math.Min(size / (double)loaded.PixelWidth, size / (double)loaded.PixelHeight);
        double width = loaded.PixelWidth * factor;
        double height = loaded.PixelHeight * factor;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(loaded, new Rect((size - width) / 2, (size - height) / 2, width, height));
        }
        var rendered = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(visual);
        var bmp = new FormatConvertedBitmap(rendered, PixelFormats.Bgra32, null, 0);
        int[] pixels = new int[size * size];
        bmp.CopyPixels(pixels, size * 4, 0);
        return pixels;
    }
}
