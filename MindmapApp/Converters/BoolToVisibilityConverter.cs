using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MindmapApp.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Ép kiểu giá trị đầu vào sang bool (mặc định là false nếu null)
            bool boolValue = value is bool b && b;

            // Kiểm tra tham số "Inverse" để đảo ngược logic
            // Nếu parameter là "Inverse": True -> Collapsed (Ẩn), False -> Visible (Hiện)
            if (parameter is string param && param.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }

            // Mặc định: True -> Visible (Hiện), False -> Collapsed (Ẩn)
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool isVisible = visibility == Visibility.Visible;

                if (parameter is string param && param.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
                {
                    return !isVisible;
                }

                return isVisible;
            }
            return false;
        }
    }
}