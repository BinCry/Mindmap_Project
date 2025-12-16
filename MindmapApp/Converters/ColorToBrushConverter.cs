using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MindmapApp.Converters;

public class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Color color ? new SolidColorBrush(color) : Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SolidColorBrush brush)
        {
            return brush.Color;
        }

        return Colors.Transparent;
    }
}
