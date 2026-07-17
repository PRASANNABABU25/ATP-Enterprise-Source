using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace atp_enterprise_app_wpf.Converters
{
    /// <summary>
    /// Converts an ATP result string (PASS/FAIL/ABORTED/PENDING) to a matching SolidColorBrush.
    /// Used in data templates to color-code status badges without code-behind.
    /// </summary>
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value?.ToString()?.ToUpperInvariant()) switch
            {
                "PASS" or "PASS" => new SolidColorBrush(Color.FromRgb(26, 128, 56)),    // green
                "FAIL"           => new SolidColorBrush(Color.FromRgb(184, 37, 37)),    // red
                "ABORTED"        => new SolidColorBrush(Color.FromRgb(194, 122, 10)),   // amber
                "PENDING"        => new SolidColorBrush(Color.FromRgb(87, 104, 122)),   // muted gray
                _                => new SolidColorBrush(Color.FromRgb(87, 104, 122))
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
