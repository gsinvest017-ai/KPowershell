using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PsTabGroups.Converters;

/// <summary>active tab 用淺色背景，inactive 用透明</summary>
[ValueConversion(typeof(bool), typeof(Brush))]
public class BoolToTabBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true
            ? new SolidColorBrush(Color.FromRgb(45, 45, 45))
            : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
