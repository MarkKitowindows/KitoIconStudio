using System.IO;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace KitoIconStudio.Core;

public sealed class IconDocument
{
    public int Width { get; set; } = 128;
    public int Height { get; set; } = 128;
    // First entry is the topmost layer.
    public ObservableCollection<LayerModel> Layers { get; } = new();
    // Stored in the same .kicon file as layers; HEX values are #RRGGBB.
    public ObservableCollection<string> PaletteColors { get; } = new();

    public static IconDocument Create(int size)
    {
        if (size is not (32 or 64 or 128 or 256))
            throw new ArgumentOutOfRangeException(nameof(size));
        var document = new IconDocument { Width = size, Height = size };
        document.Layers.Add(new LayerModel("Layer 1", size * size));
        return document;
    }

    public IconDocument DeepCopy()
    {
        var copy = new IconDocument { Width = Width, Height = Height };
        foreach (var layer in Layers)
            copy.Layers.Add(layer.Clone());
        foreach (var hex in PaletteColors)
            copy.PaletteColors.Add(hex);
        return copy;
    }

    // File format is simple, versioned JSON. The byte[] payload is Base64 in JSON.
    private sealed class DocumentFile
    {
        public int FormatVersion { get; set; } = 1;
        public int Width { get; set; }
        public int Height { get; set; }
        public List<LayerFile> Layers { get; set; } = new();
        public List<string> PaletteColors { get; set; } = new();
    }

    private sealed class LayerFile
    {
        public string Name { get; set; } = "Layer";
        public bool IsVisible { get; set; } = true;
        public byte[] Pixels { get; set; } = Array.Empty<byte>();
    }

    public void Save(string path)
    {
        var dto = new DocumentFile
        {
            Width = Width, Height = Height,
            PaletteColors = PaletteColors.ToList()
        };
        foreach (var layer in Layers)
        {
            var buffer = new byte[layer.Pixels.Length * 4];
            Buffer.BlockCopy(layer.Pixels, 0, buffer, 0, buffer.Length);
            dto.Layers.Add(new LayerFile
            {
                Name = layer.Name,
                IsVisible = layer.IsVisible,
                Pixels = buffer
            });
        }
        string json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static IconDocument Load(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > 50_000_000)
            throw new InvalidDataException("Project file is too large.");
        var dto = JsonSerializer.Deserialize<DocumentFile>(File.ReadAllText(path));
        if (dto is null || dto.FormatVersion != 1 ||
            dto.Width != dto.Height || dto.Width is not (32 or 64 or 128 or 256) ||
            dto.Layers is null || dto.Layers.Count < 1 || dto.Layers.Count > 32)
            throw new InvalidDataException("Unsupported or damaged Kito Icon Studio project.");

        // PaletteColors is optional so projects created with 0.1 still open.
        var savedColors = dto.PaletteColors ?? new List<string>();
        if (savedColors.Count > 36)
            throw new InvalidDataException("Too many colors in saved palette.");
        foreach (string? hex in savedColors)
        {
            if (hex is null || hex.Length != 7 || hex[0] != '#' ||
                !hex.Skip(1).All(Uri.IsHexDigit))
                throw new InvalidDataException("Invalid HEX color in saved palette.");
        }

        int count = checked(dto.Width * dto.Height);
        var document = new IconDocument { Width = dto.Width, Height = dto.Height };
        foreach (var item in dto.Layers)
        {
            if (item.Pixels is null || item.Pixels.Length != count * 4)
                throw new InvalidDataException("Invalid layer pixel data.");
            var pixels = new int[count];
            Buffer.BlockCopy(item.Pixels, 0, pixels, 0, item.Pixels.Length);
            document.Layers.Add(new LayerModel(
                string.IsNullOrWhiteSpace(item.Name) ? "Layer" : item.Name[..Math.Min(80, item.Name.Length)],
                count)
            {
                IsVisible = item.IsVisible,
                Pixels = pixels
            });
        }
        foreach (var hex in savedColors.Select(c => c.ToUpperInvariant()).Distinct())
            document.PaletteColors.Add(hex);
        return document;
    }
}
