using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PsTabGroups.Converters;

[ValueConversion(typeof(string), typeof(Brush))]
public class HexColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is string hex)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        catch { }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
