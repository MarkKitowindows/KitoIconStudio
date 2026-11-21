using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KitoIconStudio;

public partial class ColorPickerWindow : Window
{
    private const double SquareSize = 300;
    private double _hue;
    private double _saturation;
    private double _value;
    private bool _draggingSquare;
    private bool _draggingHue;
    private readonly Color _initialColor;
    private readonly bool _isEnglish;

    public Color SelectedColor { get; private set; }

    public ColorPickerWindow(Color initialColor, bool isEnglish = false)
    {
        InitializeComponent();
        _isEnglish = isEnglish;
        Title = _isEnglish ? "Choose color" : "Выбор цвета";
        HeadingText.Text = _isEnglish ? "Color picker" : "Выбор цвета";
        HintText.Text = _isEnglish ? "Drag on the square to choose a color." : "Перетаскивай мышь по палитре.";
        OriginalText.Text = _isEnglish ? "Before" : "Было";
        NewText.Text = _isEnglish ? "After" : "Стало";
        ApplyValuesText.Content = _isEnglish ? "Apply values" : "Применить значения";
        CancelText.Content = _isEnglish ? "Cancel" : "Отмена";
        ConfirmText.Content = _isEnglish ? "Choose" : "Выбрать";
        _initialColor = initialColor;
        SelectedColor = initialColor;
        ColorToHsv(initialColor, out _hue, out _saturation, out _value);
        UpdatePickerUi();
    }

    private static void ColorToHsv(Color color, out double hue, out double saturation, out double value)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        value = max;
        saturation = max == 0 ? 0 : delta / max;
        hue = 0;
        if (delta <= 0)
            return;

        if (max == r)
            hue = 60 * ((g - b) / delta % 6);
        else if (max == g)
            hue = 60 * ((b - r) / delta + 2);
        else
            hue = 60 * ((r - g) / delta + 4);
        if (hue < 0)
            hue += 360;
    }

    private static Color HsvToColor(double hue, double saturation, double value)
    {
        hue = ((hue % 360) + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);
        double chroma = value * saturation;
        double x = chroma * (1 - Math.Abs(hue / 60 % 2 - 1));
        double m = value - chroma;
        // Explicit double values avoid tuple/switch type-inference errors.
        double r, g, b;
        if (hue < 60.0)
        {
            r = chroma; g = x; b = 0.0;
        }
        else if (hue < 120.0)
        {
            r = x; g = chroma; b = 0.0;
        }
        else if (hue < 180.0)
        {
            r = 0.0; g = chroma; b = x;
        }
        else if (hue < 240.0)
        {
            r = 0.0; g = x; b = chroma;
        }
        else if (hue < 300.0)
        {
            r = x; g = 0.0; b = chroma;
        }
        else
        {
            r = chroma; g = 0.0; b = x;
        }
        return Color.FromRgb(ToByte(r + m), ToByte(g + m), ToByte(b + m));
    }

    private static byte ToByte(double value) =>
        (byte)Math.Round(Math.Clamp(value, 0, 1) * 255, MidpointRounding.AwayFromZero);

    private void UpdatePickerUi()
    {
        SelectedColor = HsvToColor(_hue, _saturation, _value);
        HueBase.Background = new SolidColorBrush(HsvToColor(_hue, 1, 1));
        NewColorPreview.Background = new SolidColorBrush(SelectedColor);
        OldColorPreview.Background = new SolidColorBrush(_initialColor);
        Canvas.SetLeft(SaturationMarker, _saturation * SquareSize - 8);
        Canvas.SetTop(SaturationMarker, (1 - _value) * SquareSize - 8);
        Canvas.SetLeft(HueMarker, 0);
        Canvas.SetTop(HueMarker, Math.Clamp(_hue / 360.0 * SquareSize - 4.5, -4.5, 295.5));
        HexInput.Text = $"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";
        RInput.Text = SelectedColor.R.ToString();
        GInput.Text = SelectedColor.G.ToString();
        BInput.Text = SelectedColor.B.ToString();
        ValidationText.Foreground = new SolidColorBrush(Color.FromRgb(159, 171, 202));
        ValidationText.Text = (_isEnglish ? "Drag to select a color or enter HEX/RGB." : "Выбери цвет мышью или введи HEX/RGB.");
    }

    private void SetFromColor(Color color)
    {
        ColorToHsv(color, out _hue, out _saturation, out _value);
        UpdatePickerUi();
    }

    private void SetSquarePosition(Point point)
    {
        _saturation = Math.Clamp(point.X / SquareSize, 0, 1);
        _value = 1 - Math.Clamp(point.Y / SquareSize, 0, 1);
        UpdatePickerUi();
    }

    private void SetHuePosition(Point point)
    {
        _hue = Math.Clamp(point.Y / SquareSize, 0, 1) * 360;
        UpdatePickerUi();
    }

    private void SaturationArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _draggingSquare = true;
        SaturationArea.CaptureMouse();
        SetSquarePosition(e.GetPosition(SaturationArea));
        e.Handled = true;
    }

    private void SaturationArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingSquare && e.LeftButton == MouseButtonState.Pressed)
            SetSquarePosition(e.GetPosition(SaturationArea));
    }

    private void SaturationArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _draggingSquare = false;
        SaturationArea.ReleaseMouseCapture();
    }

    private void SaturationArea_LostMouseCapture(object sender, MouseEventArgs e) =>
        _draggingSquare = false;

    private void HueArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _draggingHue = true;
        HueArea.CaptureMouse();
        SetHuePosition(e.GetPosition(HueArea));
        e.Handled = true;
    }

    private void HueArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingHue && e.LeftButton == MouseButtonState.Pressed)
            SetHuePosition(e.GetPosition(HueArea));
    }

    private void HueArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _draggingHue = false;
        HueArea.ReleaseMouseCapture();
    }

    private void HueArea_LostMouseCapture(object sender, MouseEventArgs e) =>
        _draggingHue = false;

    private void HexInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyValues(preferHex: true);
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void ApplyValues_Click(object sender, RoutedEventArgs e)
    {
        string currentHex = $"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";
        bool hexWasEdited = !string.Equals(HexInput.Text.Trim(), currentHex,
            StringComparison.OrdinalIgnoreCase);
        ApplyValues(preferHex: hexWasEdited);
    }

    private bool ApplyValues(bool preferHex)
    {
        Color color;
        if (preferHex)
        {
            if (!TryHex(HexInput.Text, out color))
            {
                ShowError(_isEnglish ? "Invalid HEX. Example: #7C3AED." : "Неверный HEX. Например: #7C3AED.");
                return false;
            }
        }
        else
        {
            if (!byte.TryParse(RInput.Text, out var r) ||
                !byte.TryParse(GInput.Text, out var g) ||
                !byte.TryParse(BInput.Text, out var b))
            {
                ShowError(_isEnglish ? "RGB must contain numbers from 0 to 255." : "RGB должен содержать числа от 0 до 255.");
                return false;
            }
            color = Color.FromRgb(r, g, b);
        }
        SetFromColor(color);
        return true;
    }

    private static bool TryHex(string? input, out Color color)
    {
        color = Colors.Black;
        var hex = (input ?? "").Trim().TrimStart('#');
        if (hex.Length == 3)
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        if (hex.Length != 6 || !hex.All(Uri.IsHexDigit))
            return false;
        color = Color.FromRgb(Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16));
        return true;
    }

    private void ShowError(string message)
    {
        ValidationText.Foreground = new SolidColorBrush(Color.FromRgb(255, 160, 162));
        ValidationText.Text = message;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        // When a value field has focus, respect any just-entered code.
        if (HexInput.IsKeyboardFocusWithin && !ApplyValues(preferHex: true))
            return;
        if ((RInput.IsKeyboardFocusWithin || GInput.IsKeyboardFocusWithin || BInput.IsKeyboardFocusWithin)
            && !ApplyValues(preferHex: false))
            return;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
