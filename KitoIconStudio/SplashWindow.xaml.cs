using System.Windows;
using System.Windows.Media.Animation;

namespace KitoIconStudio;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var animation = new DoubleAnimation(-290, 0, TimeSpan.FromMilliseconds(880));
            ProgressShift.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, animation);
        };
    }
}
