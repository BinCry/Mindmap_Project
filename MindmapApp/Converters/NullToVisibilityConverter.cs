using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace MindmapApp.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null;

            // Nếu tham số là "Inverse" -> Đảo ngược logic (Null thì Hiện, Có thì Ẩn)
            if (parameter is string param && param == "Inverse")
            {
                return isNull ? Visibility.Visible : Visibility.Collapsed;
            }

            // Mặc định: Null thì Ẩn, Có thì Hiện
            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
