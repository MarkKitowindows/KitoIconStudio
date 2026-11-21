using System.IO;
using Microsoft.Win32;
using KitoIconStudio.Core;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KitoIconStudio;

public partial class MainWindow : Window
{
    private enum DrawingTool { Brush, Pencil, Eraser, Fill, Line, Rectangle, Ellipse, Eyedropper }
    private sealed record HistoryState(IconDocument Document, int LayerIndex);

    private IconDocument _document = IconDocument.Create(128);
    private WriteableBitmap? _preview;
    private readonly Stack<HistoryState> _undo = new();
    private readonly Stack<HistoryState> _redo = new();
    private DrawingTool _currentTool = DrawingTool.Brush;
    private Color _color = Color.FromRgb(120, 87, 232);
    private string? _projectPath;
    private bool _dirty;
    private string _language = "ru";
    private bool _drawing;
    private int _startX, _startY, _lastX, _lastY;
    private int[]? _shapeOriginal;
    private const int PaletteLimit = 36;
    private string? _selectedPaletteHex;

    private LayerModel? CurrentLayer => LayerList.SelectedItem as LayerModel;
    private bool IsEnglish => _language == "en";
    private string T(string ru, string en) => IsEnglish ? en : ru;

    public MainWindow()
    {
        InitializeComponent();
        InlinePicker.ColorChanged += (_, _) => SetColor(InlinePicker.SelectedColor);
        LoadLanguage();
        SetDocument(_document, 0);
        SetColor(_color);
        SetTool(DrawingTool.Brush);
        ApplyLanguage();
        Status(T("Готово к работе", "Ready to create"));
    }

    private void LoadLanguage()
    {
        try
        {
            var file = LanguageFile();
            if (File.Exists(file) && File.ReadAllText(file).Trim() == "en")
                _language = "en";
        }
        catch { _language = "ru"; }
    }

    private static string LanguageFile() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KitoIconStudio", "language.txt");

    private void SaveLanguage()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LanguageFile())!);
            File.WriteAllText(LanguageFile(), _language);
        }
        catch { /* Language remains usable even when preferences cannot be saved. */ }
    }

    private void ApplyLanguage()
    {
        FileMenu.Header = T("Файл", "File");
        EditMenu.Header = T("Правка", "Edit");
        ViewMenu.Header = T("Вид", "View");
        HelpMenu.Header = T("Справка", "Help");
        MenuNew.Header = T("Создать...", "New...");
        MenuOpen.Header = T("Открыть проект...", "Open project...");
        MenuSave.Header = T("Сохранить проект", "Save project");
        MenuImport.Header = T("Импорт изображения...", "Import image...");
        MenuPng.Header = T("Экспорт PNG...", "Export PNG...");
        MenuIco.Header = T("Экспорт ICO...", "Export ICO...");
        MenuExit.Header = T("Выход", "Exit");
        MenuUndo.Header = T("Отменить", "Undo");
        MenuRedo.Header = T("Повторить", "Redo");
        MenuClear.Header = T("Очистить слой", "Clear layer");
        MenuAbout.Header = T("О программе", "About");
        MenuRussian.IsChecked = !IsEnglish;
        MenuEnglish.IsChecked = IsEnglish;
        NewText.Text = T("Создать", "New");
        OpenText.Text = T("Открыть", "Open");
        SaveText.Text = T("Сохранить", "Save");
        ImportText.Text = T("Импорт PNG / JPG", "Import PNG / JPG");
        ExportIcoText.Text = T("Экспорт ICO", "Export ICO");
        ExportPngText.Text = T("Экспорт PNG", "Export PNG");
        UndoBtn.ToolTip = T("Отменить (Ctrl+Z)", "Undo (Ctrl+Z)");
        RedoBtn.ToolTip = T("Повторить (Ctrl+Y)", "Redo (Ctrl+Y)");
        ToolsHeading.Text = T("ИНСТРУМЕНТЫ", "TOOLS");
        BrushLabel.Text = T("Кисть", "Brush");
        PencilLabel.Text = T("Карандаш", "Pencil");
        EraserLabel.Text = T("Ластик", "Eraser");
        FillLabel.Text = T("Заливка", "Fill");
        LineLabel.Text = T("Линия", "Line");
        RectangleLabel.Text = T("Прямоугольник", "Rectangle");
        EllipseLabel.Text = T("Эллипс", "Ellipse");
        EyedropperLabel.Text = T("Пипетка", "Eyedropper");
        CanvasHeading.Text = T("Холст", "Canvas");
        CanvasCaption.Text = T("Прозрачный фон · пиксельный редактор", "Transparent background · pixel editor");
        CanvasInstruction.Text = T("Рисуй мышью · Ctrl+Z — отмена · Ctrl+S — сохранить",
            "Draw with the mouse · Ctrl+Z to undo · Ctrl+S to save");
        ColorHeading.Text = T("ЦВЕТ", "COLOR");
        InlinePickerHeading.Text = T("ПАЛИТРА ЦВЕТОВ", "COLOR PICKER");
        InlinePickerHint.Text = T("Слева — насыщенность и яркость, справа — оттенок",
            "Choose saturation and brightness on the left, hue on the right");
        SavedColorsExpander.Header = T("Сохранённые цвета", "Saved colors");
        ColorPickerText.Text = T("Выбрать цвет...", "Choose color...");
        PaletteHeading.Text = T("МОЯ ПАЛИТРА", "MY PALETTE");
        PaletteAddText.Text = T("+ Добавить", "+ Add color");
        PaletteRemoveText.Text = T("− Удалить", "− Remove");
        PaletteEmptyText.Text = T("Добавь свой первый цвет", "Add your first color");
        PaletteHintText.Text = T("Нажми на цвет, чтобы выбрать его. Палитра сохраняется в проекте.",
            "Click a color to select it. The palette is saved with your project.");
        RefreshPaletteUi();
        BrushSizeHeading.Text = T("Размер кисти", "Brush size");
        LayersHeading.Text = T("СЛОИ", "LAYERS");
        RenameHeading.Text = T("Имя слоя", "Layer name");
        LayersHint.Text = T("Верхний слой перекрывает нижние.", "The top layer covers lower layers.");
        ShortcutsHint.Text = IsEnglish ? "B · brush\nE · eraser\nG · fill\nI · picker" :
            "B · кисть\nE · ластик\nG · заливка\nI · пипетка";
        UpdateToolLabels();
        UpdateWindowTitle();
    }

    private string ToolLabel(DrawingTool tool) => tool switch
    {
        DrawingTool.Brush => T("Кисть", "Brush"),
        DrawingTool.Pencil => T("Карандаш", "Pencil"),
        DrawingTool.Eraser => T("Ластик", "Eraser"),
        DrawingTool.Fill => T("Заливка", "Bucket fill"),
        DrawingTool.Line => T("Линия", "Line"),
        DrawingTool.Rectangle => T("Прямоугольник", "Rectangle"),
        DrawingTool.Ellipse => T("Эллипс", "Ellipse"),
        DrawingTool.Eyedropper => T("Пипетка", "Eyedropper"),
        _ => "Tool"
    };

    private void UpdateToolLabels()
    {
        CurrentToolCaption.Text = ToolLabel(_currentTool);
        foreach (Button button in ToolPanel.Children.OfType<Button>())
        {
            if (Enum.TryParse<DrawingTool>(button.Tag as string, out var tool))
            {
                button.ToolTip = ToolLabel(tool);
                button.Background = new SolidColorBrush(tool == _currentTool
                    ? Color.FromRgb(95, 74, 186)
                    : Color.FromRgb(29, 39, 59));
            }
        }
    }

    private void SetTool(DrawingTool tool)
    {
        _currentTool = tool;
        UpdateToolLabels();
        CanvasSurface.Cursor = tool == DrawingTool.Eyedropper ? Cursors.Hand : Cursors.Cross;
    }

    private void Tool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && Enum.TryParse<DrawingTool>(tag, out var tool))
            SetTool(tool);
    }

    private void SetColor(Color color)
    {
        _color = Color.FromRgb(color.R, color.G, color.B);
        HexInput.Text = $"#{_color.R:X2}{_color.G:X2}{_color.B:X2}";
        ColorPreview.Background = new SolidColorBrush(_color);
        // Keep the inline HSV picker in sync with HEX, pipette and swatches.
        if (InlinePicker != null && InlinePicker.SelectedColor != _color)
            InlinePicker.SetColor(_color);
        RefreshPaletteUi();
    }

    private static bool ParseHex(string? text, out Color color)
    {
        color = Colors.Black;
        string hex = (text ?? "").Trim().TrimStart('#');
        if (hex.Length == 3)
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        if (hex.Length != 6 || !hex.All(Uri.IsHexDigit)) return false;
        color = Color.FromRgb(Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16));
        return true;
    }

    private void ApplyHex_Click(object sender, RoutedEventArgs e)
    {
        if (ParseHex(HexInput.Text, out Color color))
            SetColor(color);
        else
        {
            HexInput.Text = $"#{_color.R:X2}{_color.G:X2}{_color.B:X2}";
            Status(T("Неверный HEX (пример: #7857E8)", "Invalid HEX (example: #7857E8)"), true);
        }
    }

    private void HexInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyHex_Click(sender, e);
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void PickColor_Click(object sender, RoutedEventArgs e)
    {
        var picker = new ColorPickerWindow(_color, IsEnglish) { Owner = this };
        if (picker.ShowDialog() == true)
            SetColor(picker.SelectedColor);
    }

    private void Swatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hex } && ParseHex(hex, out Color color))
            SetColor(color);
    }

    private string CurrentHex() => $"#{_color.R:X2}{_color.G:X2}{_color.B:X2}";

    private void AddPaletteColor_Click(object sender, RoutedEventArgs e)
    {
        string hex = CurrentHex();
        if (_document.PaletteColors.Contains(hex))
        {
            _selectedPaletteHex = hex;
            RefreshPaletteUi();
            Status(T("Этот цвет уже в палитре", "This color is already in the palette"));
            return;
        }
        if (_document.PaletteColors.Count >= PaletteLimit)
        {
            Status(T("Палитра заполнена (36 цветов)", "Palette is full (36 colors)"), true);
            return;
        }
        RecordUndo();
        _document.PaletteColors.Add(hex);
        _selectedPaletteHex = hex;
        RefreshPaletteUi();
        MarkDirty();
        Status(T("Цвет добавлен в палитру", "Color added to palette"));
    }

    private void SelectPaletteColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hex } && ParseHex(hex, out Color color))
        {
            SetColor(color);
            _selectedPaletteHex = hex;
            RefreshPaletteUi();
            Status(T("Цвет выбран из палитры", "Palette color selected"));
        }
    }

    private void RemovePaletteColor_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPaletteHex is not null)
            RemovePaletteColor(_selectedPaletteHex);
    }

    private void RemovePaletteColor(string hex)
    {
        if (!_document.PaletteColors.Contains(hex)) return;
        RecordUndo();
        _document.PaletteColors.Remove(hex);
        _selectedPaletteHex = null;
        RefreshPaletteUi();
        MarkDirty();
        Status(T("Цвет удалён из палитры", "Color removed from palette"));
    }

    private void RefreshPaletteUi()
    {
        if (UserPalettePanel is null) return;
        if (_selectedPaletteHex is not null && !_document.PaletteColors.Contains(_selectedPaletteHex))
            _selectedPaletteHex = null;

        PaletteCountText.Text = $"{_document.PaletteColors.Count} / {PaletteLimit}";
        PaletteEmptyText.Visibility = _document.PaletteColors.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;
        PaletteRemoveButton.IsEnabled = _selectedPaletteHex is not null;
        PaletteAddButton.IsEnabled = _document.PaletteColors.Count < PaletteLimit ||
                                     _document.PaletteColors.Contains(CurrentHex());
        UserPalettePanel.Children.Clear();
        foreach (var hex in _document.PaletteColors)
        {
            if (!ParseHex(hex, out var color)) continue;
            var button = new Button
            {
                Style = (Style)FindResource("PaletteCell"),
                Background = new SolidColorBrush(color),
                BorderBrush = hex == _selectedPaletteHex
                    ? Brushes.White : new SolidColorBrush(Color.FromRgb(91, 109, 143)),
                BorderThickness = new Thickness(hex == _selectedPaletteHex ? 3 : 1),
                Tag = hex,
                ToolTip = hex + T(" — нажми, чтобы выбрать", " — click to select")
            };
            button.Click += SelectPaletteColor_Click;
            // A context menu also lets users remove a swatch directly.
            var menu = new ContextMenu();
            var remove = new MenuItem { Header = T("Удалить цвет", "Remove color") };
            string capturedHex = hex;
            remove.Click += (_, _) => RemovePaletteColor(capturedHex);
            menu.Items.Add(remove);
            button.ContextMenu = menu;
            UserPalettePanel.Children.Add(button);
        }
    }

    private void BrushSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BrushSizeText != null)
            BrushSizeText.Text = $"{(int)BrushSizeSlider.Value} px";
    }

    private void SetDocument(IconDocument document, int selectedIndex)
    {
        foreach (var old in _document.Layers)
            old.PropertyChanged -= OnLayerPropertyChanged;
        _document = document;
        _selectedPaletteHex = null;
        foreach (var layer in _document.Layers)
            layer.PropertyChanged += OnLayerPropertyChanged;
        LayerList.ItemsSource = _document.Layers;
        LayerList.SelectedIndex = Math.Clamp(selectedIndex, 0, _document.Layers.Count - 1);
        _preview = new WriteableBitmap(_document.Width, _document.Height,
            96, 96, PixelFormats.Bgra32, null);
        CanvasImage.Source = _preview;
        DimensionsText.Text = $"{_document.Width} × {_document.Height}";
        CanvasSizeText.Text = $"{_document.Width} × {_document.Height} px";
        UpdateLayerControls();
        RefreshPaletteUi();
        Render();
    }

    private void OnLayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayerModel.IsVisible))
        {
            MarkDirty();
            Render();
        }
    }

    private void UpdateLayerControls()
    {
        LayerCountText.Text = _document.Layers.Count.ToString();
        LayerNameInput.Text = CurrentLayer?.Name ?? "";
    }

    private void Render()
    {
        if (_preview is null) return;
        int[] composite = RasterEngine.Composite(_document);
        _preview.WritePixels(new Int32Rect(0, 0, _document.Width, _document.Height),
            composite, _document.Width * 4, 0);
    }

    private void UpdateWindowTitle()
    {
        string title = string.IsNullOrWhiteSpace(_projectPath) ? T("Без названия", "Untitled") :
            Path.GetFileName(_projectPath);
        Title = $"Kito Icon Studio — {title}{(_dirty ? " *" : "")}";
    }

    private void MarkDirty()
    {
        _dirty = true;
        UpdateWindowTitle();
    }

    private void Status(string message, bool error = false)
    {
        StatusMessage.Text = message;
        StatusMessage.Foreground = new SolidColorBrush(error
            ? Color.FromRgb(255, 149, 166) : Color.FromRgb(150, 220, 195));
    }

    private void RecordUndo()
    {
        _undo.Push(new HistoryState(_document.DeepCopy(), LayerList.SelectedIndex));
        if (_undo.Count > 30)
        {
            var recent = _undo.ToArray().Take(30).Reverse().ToArray();
            _undo.Clear();
            foreach (var state in recent) _undo.Push(state);
        }
        _redo.Clear();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_undo.Count == 0) { Status(T("Нечего отменять", "Nothing to undo")); return; }
        _redo.Push(new HistoryState(_document.DeepCopy(), LayerList.SelectedIndex));
        HistoryState state = _undo.Pop();
        SetDocument(state.Document, state.LayerIndex);
        MarkDirty();
        Status(T("Действие отменено", "Action undone"));
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (_redo.Count == 0) { Status(T("Нечего повторять", "Nothing to redo")); return; }
        _undo.Push(new HistoryState(_document.DeepCopy(), LayerList.SelectedIndex));
        HistoryState state = _redo.Pop();
        SetDocument(state.Document, state.LayerIndex);
        MarkDirty();
        Status(T("Действие повторено", "Action redone"));
    }

    private bool ConfirmDiscard()
    {
        if (!_dirty) return true;
        return MessageBox.Show(
            T("Есть несохранённые изменения. Продолжить без сохранения?",
              "There are unsaved changes. Continue without saving?"),
            "Kito Icon Studio", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private int? AskDocumentSize()
    {
        var window = new Window
        {
            Title = T("Новый проект", "New project"), Width = 370, Height = 254,
            ResizeMode = ResizeMode.NoResize, Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(23, 31, 48)), Foreground = Brushes.White
        };
        var panel = new StackPanel { Margin = new Thickness(27) };
        panel.Children.Add(new TextBlock
        {
            Text = T("Размер нового холста", "New canvas size"),
            FontSize = 21, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 17)
        });
        var combo = new ComboBox { Height = 34, Margin = new Thickness(0, 0, 0, 15) };
        foreach (int size in new[] { 32, 64, 128, 256 })
            combo.Items.Add($"{size} × {size} px");
        combo.SelectedIndex = 2;
        panel.Children.Add(combo);
        panel.Children.Add(new TextBlock
        {
            Text = T("Прозрачный фон, один пустой слой.", "Transparent canvas with one empty layer."),
            Foreground = new SolidColorBrush(Color.FromRgb(173, 186, 209)),
            FontSize = 12, Margin = new Thickness(0, 0, 0, 17)
        });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = T("Отмена", "Cancel"), Padding = new Thickness(13, 7, 13, 7), Margin = new Thickness(0, 0, 8, 0) };
        var create = new Button { Content = T("Создать", "Create"), Padding = new Thickness(20, 7, 20, 7), IsDefault = true };
        cancel.Click += (_, _) => window.DialogResult = false;
        create.Click += (_, _) => window.DialogResult = true;
        actions.Children.Add(cancel); actions.Children.Add(create); panel.Children.Add(actions);
        window.Content = panel;
        return window.ShowDialog() == true ? new[] { 32, 64, 128, 256 }[combo.SelectedIndex] : null;
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        int? size = AskDocumentSize();
        if (!size.HasValue) return;
        _undo.Clear(); _redo.Clear(); _projectPath = null; _dirty = false;
        SetDocument(IconDocument.Create(size.Value), 0);
        UpdateWindowTitle();
        Status(T("Создан новый проект", "New project created"));
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        var dialog = new OpenFileDialog { Filter = "Kito Icon Studio (*.kicon)|*.kicon", Title = T("Открыть проект", "Open project") };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var document = IconDocument.Load(dialog.FileName);
            _undo.Clear(); _redo.Clear(); _projectPath = dialog.FileName; _dirty = false;
            SetDocument(document, 0);
            UpdateWindowTitle();
            Status(T("Проект открыт", "Project opened"));
        }
        catch (Exception ex)
        {
            MessageBox.Show(T("Не удалось открыть проект:\n", "Could not open project:\n") + ex.Message,
                "Kito Icon Studio", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string? path = _projectPath;
        if (path is null)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Kito Icon Studio (*.kicon)|*.kicon", DefaultExt = ".kicon",
                FileName = "MyIcon.kicon", Title = T("Сохранить проект", "Save project")
            };
            if (dialog.ShowDialog(this) != true) return;
            path = dialog.FileName;
        }
        try
        {
            _document.Save(path);
            _projectPath = path; _dirty = false;
            UpdateWindowTitle();
            Status(T("Проект сохранён", "Project saved"));
        }
        catch (Exception ex)
        {
            MessageBox.Show(T("Не удалось сохранить проект:\n", "Could not save project:\n") + ex.Message,
                "Kito Icon Studio", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "PNG Image (*.png)|*.png", DefaultExt = ".png", FileName = "Icon.png" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            ExportService.ExportPng(_document, dialog.FileName);
            Status(T("PNG экспортирован", "PNG exported"));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "PNG", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportIco_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Windows Icon (*.ico)|*.ico", DefaultExt = ".ico", FileName = "Icon.ico" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            ExportService.ExportIco(_document, dialog.FileName);
            Status(T("ICO экспортирован (16/32/48/256 px)", "ICO exported (16/32/48/256 px)"));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ICO", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (_document.Layers.Count >= 32)
            {
                Status(T("Достигнут предел: 32 слоя", "Maximum of 32 layers reached"), true);
                return;
            }
            var pixels = ExportService.ImportImage(dialog.FileName, _document.Width);
            RecordUndo();
            var layer = new LayerModel(Path.GetFileNameWithoutExtension(dialog.FileName), pixels.Length)
            {
                Pixels = pixels
            };
            layer.PropertyChanged += OnLayerPropertyChanged;
            _document.Layers.Insert(0, layer);
            LayerList.SelectedIndex = 0;
            UpdateLayerControls(); Render(); MarkDirty();
            Status(T("Изображение импортировано как новый слой", "Image imported as a new layer"));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Kito Icon Studio", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LayerList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateLayerControls();

    private void AddLayer_Click(object sender, RoutedEventArgs e)
    {
        if (_document.Layers.Count >= 32)
        {
            Status(T("Максимум 32 слоя", "Maximum 32 layers"), true); return;
        }
        RecordUndo();
        var layer = new LayerModel($"Layer {_document.Layers.Count + 1}", _document.Width * _document.Height);
        layer.PropertyChanged += OnLayerPropertyChanged;
        _document.Layers.Insert(0, layer);
        LayerList.SelectedIndex = 0;
        UpdateLayerControls(); MarkDirty();
        Status(T("Слой добавлен", "Layer added"));
    }

    private void DeleteLayer_Click(object sender, RoutedEventArgs e)
    {
        var layer = CurrentLayer;
        if (layer is null) return;
        if (_document.Layers.Count == 1)
        {
            Status(T("Нельзя удалить последний слой", "Cannot delete the last layer"), true);
            return;
        }
        RecordUndo();
        int index = LayerList.SelectedIndex;
        layer.PropertyChanged -= OnLayerPropertyChanged;
        _document.Layers.RemoveAt(index);
        LayerList.SelectedIndex = Math.Min(index, _document.Layers.Count - 1);
        UpdateLayerControls(); Render(); MarkDirty();
        Status(T("Слой удалён", "Layer deleted"));
    }

    private void MoveLayerUp_Click(object sender, RoutedEventArgs e) => MoveLayer(-1);
    private void MoveLayerDown_Click(object sender, RoutedEventArgs e) => MoveLayer(1);

    private void MoveLayer(int direction)
    {
        int index = LayerList.SelectedIndex;
        int target = index + direction;
        if (index < 0 || target < 0 || target >= _document.Layers.Count) return;
        RecordUndo();
        _document.Layers.Move(index, target);
        LayerList.SelectedIndex = target;
        Render(); MarkDirty();
    }

    private void RenameLayer_Click(object sender, RoutedEventArgs e)
    {
        var layer = CurrentLayer;
        string name = LayerNameInput.Text.Trim();
        if (layer is null || name.Length == 0 || name == layer.Name) return;
        RecordUndo();
        layer.Name = name[..Math.Min(name.Length, 80)];
        MarkDirty();
    }

    private void ClearLayer_Click(object sender, RoutedEventArgs e)
    {
        var layer = CurrentLayer;
        if (layer is null) return;
        RecordUndo();
        Array.Clear(layer.Pixels);
        Render(); MarkDirty();
        Status(T("Слой очищен", "Layer cleared"));
    }

    private (int X, int Y) MousePixel(MouseEventArgs e)
    {
        Point p = e.GetPosition(CanvasSurface);
        int x = Math.Clamp((int)(p.X / CanvasSurface.ActualWidth * _document.Width), 0, _document.Width - 1);
        int y = Math.Clamp((int)(p.Y / CanvasSurface.ActualHeight * _document.Height), 0, _document.Height - 1);
        return (x, y);
    }

    private bool InCanvas(MouseEventArgs e)
    {
        Point p = e.GetPosition(CanvasSurface);
        return p.X >= 0 && p.Y >= 0 && p.X < CanvasSurface.ActualWidth && p.Y < CanvasSurface.ActualHeight;
    }

    private int CurrentPixelColor() => _currentTool == DrawingTool.Eraser ? 0 : RasterEngine.ToPixel(_color);
    private int BrushRadius() => _currentTool == DrawingTool.Pencil ? 0 : Math.Max(0, ((int)BrushSizeSlider.Value - 1) / 2);

    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!InCanvas(e)) return;
        var layer = CurrentLayer;
        if (layer is null) return;
        var (x, y) = MousePixel(e);
        if (_currentTool == DrawingTool.Eyedropper)
        {
            var composite = RasterEngine.Composite(_document);
            var color = RasterEngine.ToColor(composite[y * _document.Width + x]);
            if (color.A > 0) SetColor(color);
            Status(T("Цвет взят пипеткой", "Color picked"));
            return;
        }
        if (!layer.IsVisible)
        {
            Status(T("Включи видимость выбранного слоя", "Make the selected layer visible"), true);
            return;
        }
        RecordUndo();
        _startX = _lastX = x;
        _startY = _lastY = y;
        if (_currentTool == DrawingTool.Fill)
        {
            RasterEngine.FloodFill(layer.Pixels, _document.Width, x, y, CurrentPixelColor());
            Render(); MarkDirty();
            return;
        }
        _drawing = true;
        _shapeOriginal = _currentTool is DrawingTool.Line or DrawingTool.Rectangle or DrawingTool.Ellipse
            ? (int[])layer.Pixels.Clone() : null;
        CanvasSurface.CaptureMouse();
        DrawDrag(x, y);
        e.Handled = true;
    }

    private void DrawDrag(int x, int y)
    {
        var layer = CurrentLayer;
        if (layer is null) return;
        int radius = BrushRadius();
        int ink = CurrentPixelColor();
        if (_shapeOriginal is not null)
            Array.Copy(_shapeOriginal, layer.Pixels, layer.Pixels.Length);

        switch (_currentTool)
        {
            case DrawingTool.Brush:
            case DrawingTool.Pencil:
            case DrawingTool.Eraser:
                RasterEngine.Line(layer.Pixels, _document.Width, _lastX, _lastY, x, y, radius, ink);
                break;
            case DrawingTool.Line:
                RasterEngine.Line(layer.Pixels, _document.Width, _startX, _startY, x, y, radius, ink);
                break;
            case DrawingTool.Rectangle:
                RasterEngine.Rectangle(layer.Pixels, _document.Width, _startX, _startY, x, y, radius, ink);
                break;
            case DrawingTool.Ellipse:
                RasterEngine.Ellipse(layer.Pixels, _document.Width, _startX, _startY, x, y, radius, ink);
                break;
        }
        _lastX = x; _lastY = y;
        Render(); MarkDirty();
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        var (x, y) = MousePixel(e);
        PositionText.Text = $"X: {x}   Y: {y}";
        if (_drawing && e.LeftButton == MouseButtonState.Pressed)
            DrawDrag(x, y);
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_drawing) return;
        var (x, y) = MousePixel(e);
        DrawDrag(x, y);
        _drawing = false;
        _shapeOriginal = null;
        CanvasSurface.ReleaseMouseCapture();
        Status(T("Изменения внесены", "Canvas updated"));
    }

    private void Canvas_RightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!InCanvas(e)) return;
        var (x, y) = MousePixel(e);
        Color sample = RasterEngine.ToColor(RasterEngine.Composite(_document)[y * _document.Width + x]);
        if (sample.A > 0) SetColor(sample);
        e.Handled = true;
    }

    private void Russian_Click(object sender, RoutedEventArgs e) { _language = "ru"; SaveLanguage(); ApplyLanguage(); }
    private void English_Click(object sender, RoutedEventArgs e) { _language = "en"; SaveLanguage(); ApplyLanguage(); }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(T(
            "Kito Icon Studio 0.2 Beta\n\nРедактор иконок для Windows.\nC# + WPF + .NET 10\n\n© 2026 Kito Corporation",
            "Kito Icon Studio 0.2 Beta\n\nIcon editor for Windows.\nC# + WPF + .NET 10\n\n© 2026 Kito Corporation"),
            T("О программе", "About"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
    private void MainWindow_Closing(object? sender, CancelEventArgs e) => e.Cancel = !ConfirmDiscard();

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.S: Save_Click(sender, e); e.Handled = true; return;
                case Key.O: Open_Click(sender, e); e.Handled = true; return;
                case Key.N: New_Click(sender, e); e.Handled = true; return;
                case Key.Z:
                    if (Keyboard.FocusedElement is not TextBox) { Undo_Click(sender, e); e.Handled = true; }
                    return;
                case Key.Y:
                    if (Keyboard.FocusedElement is not TextBox) { Redo_Click(sender, e); e.Handled = true; }
                    return;
            }
        }
        if (Keyboard.Modifiers != ModifierKeys.None || Keyboard.FocusedElement is TextBox) return;
        var tool = e.Key switch
        {
            Key.B => DrawingTool.Brush,
            Key.E => DrawingTool.Eraser,
            Key.G => DrawingTool.Fill,
            Key.I => DrawingTool.Eyedropper,
            _ => _currentTool
        };
        if (tool != _currentTool)
        {
            SetTool(tool); e.Handled = true;
        }
    }
}
