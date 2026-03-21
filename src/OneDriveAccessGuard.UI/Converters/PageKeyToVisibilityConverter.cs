using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OneDriveAccessGuard.UI.Converters;

/// <summary>
/// CurrentPageKey (string) と ConverterParameter (string) を比較し、
/// 一致すれば Visible、不一致なら Collapsed を返す。
/// </summary>
[ValueConversion(typeof(string), typeof(Visibility))]
public class PageKeyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
