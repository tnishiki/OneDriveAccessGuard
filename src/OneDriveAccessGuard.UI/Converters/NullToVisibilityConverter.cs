using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OneDriveAccessGuard.UI.Converters;

/// <summary>
/// null → Collapsed、非null → Visible。
/// ConverterParameter="Inverse" を指定すると逆になる。
/// </summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isNull = value == null;
        bool inverse = parameter?.ToString() == "Inverse";
        return (isNull == inverse) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
