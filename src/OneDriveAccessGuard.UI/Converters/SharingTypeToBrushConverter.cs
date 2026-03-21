using OneDriveAccessGuard.Core.Enums;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OneDriveAccessGuard.UI.Converters;

[ValueConversion(typeof(SharingType), typeof(Brush))]
public class SharingTypeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is SharingType type ? type switch
        {
            SharingType.AnonymousLink   => new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)),
            SharingType.ExternalUser    => new SolidColorBrush(Color.FromRgb(0xEF, 0x6C, 0x00)),
            SharingType.OrganizationLink => new SolidColorBrush(Color.FromRgb(0xF9, 0xA8, 0x25)),
            SharingType.SpecificPeople  => new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0)),
            _                           => new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75))
        } : Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
