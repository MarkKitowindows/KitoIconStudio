using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KitoIconStudio;

/// <summary>
/// Interactive HSV color field for the inspector: left-right saturation,
/// top-bottom brightness, with a separate vertical hue slider.
/// </summary>
public partial class InlineColorPicker : UserControl
{
    private double _hue;
    private double _saturation;
    private double _brightness;
    private bool _squareDragging;
    private bool _hueDragging;

    public Color SelectedColor { get; private set; } = Color.FromRgb(120, 87, 232);
    public event EventHandler? ColorChanged;

    public InlineColorPicker()
    {
        InitializeComponent();
        SetColor(SelectedColor);
    }

    public void SetColor(Color color)
    {
        SelectedColor = Color.FromRgb(color.R, color.G, color.B);
        RgbToHsv(SelectedColor, out _hue, out _saturation, out _brightness);
        UpdateVisuals();
    }

    private void SetFromPointer()
    {
        SelectedColor = HsvToRgb(_hue, _saturation, _brightness);
        UpdateVisuals();
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateVisuals()
    {
        if (HueColorBackground is null || SaturationMarker is null || HueMarker is null)
            return;

        HueColorBackground.Fill = new SolidColorBrush(HsvToRgb(_hue, 1, 1));
        double width = SaturationArea.ActualWidth;
        double height = SaturationArea.ActualHeight;
        if (width > 0 && height > 0)
        {
            Canvas.SetLeft(SaturationMarker, _saturation * width - SaturationMarker.Width / 2);
            Canvas.SetTop(SaturationMarker, (1 - _brightness) * height - SaturationMarker.Height / 2);
        }

        if (HueArea.ActualHeight > 0)
        {
            HueMarker.Width = HueArea.ActualWidth;
            Canvas.SetTop(HueMarker, _hue / 360 * HueArea.ActualHeight - HueMarker.Height / 2);
        }
    }

    private void ColorFields_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateVisuals();

    private void ChangeSaturation(Point point)
    {
        if (SaturationArea.ActualWidth <= 0 || SaturationArea.ActualHeight <= 0)
            return;
        _saturation = Math.Clamp(point.X / SaturationArea.ActualWidth, 0, 1);
        _brightness = 1 - Math.Clamp(point.Y / SaturationArea.ActualHeight, 0, 1);
        SetFromPointer();
    }

    private void ChangeHue(Point point)
    {
        if (HueArea.ActualHeight <= 0)
            return;
        _hue = Math.Clamp(point.Y / HueArea.ActualHeight, 0, 1) * 360;
        SetFromPointer();
    }

    private void Saturation_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _squareDragging = true;
        SaturationArea.CaptureMouse();
        ChangeSaturation(e.GetPosition(SaturationArea));
        e.Handled = true;
    }

    private void Saturation_MouseMove(object sender, MouseEventArgs e)
    {
        if (_squareDragging && e.LeftButton == MouseButtonState.Pressed)
            ChangeSaturation(e.GetPosition(SaturationArea));
    }

    private void Saturation_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_squareDragging)
        {
            ChangeSaturation(e.GetPosition(SaturationArea));
            _squareDragging = false;
            SaturationArea.ReleaseMouseCapture();
        }
    }

    private void Saturation_LostMouseCapture(object sender, MouseEventArgs e) => _squareDragging = false;

    private void Hue_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _hueDragging = true;
        HueArea.CaptureMouse();
        ChangeHue(e.GetPosition(HueArea));
        e.Handled = true;
    }

    private void Hue_MouseMove(object sender, MouseEventArgs e)
    {
        if (_hueDragging && e.LeftButton == MouseButtonState.Pressed)
            ChangeHue(e.GetPosition(HueArea));
    }

    private void Hue_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_hueDragging)
        {
            ChangeHue(e.GetPosition(HueArea));
            _hueDragging = false;
            HueArea.ReleaseMouseCapture();
        }
    }

    private void Hue_LostMouseCapture(object sender, MouseEventArgs e) => _hueDragging = false;

    private static void RgbToHsv(Color c, out double h, out double s, out double v)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;
        v = max;
        s = max <= 0 ? 0 : delta / max;
        h = 0;
        if (delta <= 0) return;
        if (max == r) h = 60 * (((g - b) / delta) % 6);
        else if (max == g) h = 60 * ((b - r) / delta + 2);
        else h = 60 * ((r - g) / delta + 4);
        if (h < 0) h += 360;
    }

    private static Color HsvToRgb(double hue, double saturation, double brightness)
    {
        hue = (hue % 360 + 360) % 360;
        double chroma = brightness * saturation;
        double x = chroma * (1 - Math.Abs(hue / 60 % 2 - 1));
        double m = brightness - chroma;
        double r, g, b;
        if (hue < 60) { r = chroma; g = x; b = 0; }
        else if (hue < 120) { r = x; g = chroma; b = 0; }
        else if (hue < 180) { r = 0; g = chroma; b = x; }
        else if (hue < 240) { r = 0; g = x; b = chroma; }
        else if (hue < 300) { r = x; g = 0; b = chroma; }
        else { r = chroma; g = 0; b = x; }
        return Color.FromRgb((byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}
