using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KitoIconStudio.Core;

public sealed class LayerModel : INotifyPropertyChanged
{
    private string _name;
    private bool _visible = true;

    public LayerModel(string name, int pixelCount)
    {
        _name = name;
        Pixels = new int[pixelCount];
    }

    public string Name
    {
        get => _name;
        set { _name = value; Notify(); }
    }

    public bool IsVisible
    {
        get => _visible;
        set { _visible = value; Notify(); }
    }

    public int[] Pixels { get; set; }

    public LayerModel Clone()
    {
        return new LayerModel(Name, Pixels.Length)
        {
            IsVisible = IsVisible,
            Pixels = (int[])Pixels.Clone()
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
