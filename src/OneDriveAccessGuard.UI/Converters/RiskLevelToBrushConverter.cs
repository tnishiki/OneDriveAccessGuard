using OneDriveAccessGuard.Core.Enums;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OneDriveAccessGuard.UI.Converters;

[ValueConversion(typeof(RiskLevel), typeof(Brush))]
public class RiskLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is RiskLevel level ? level switch
        {
            RiskLevel.High   => new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)),
            RiskLevel.Medium => new SolidColorBrush(Color.FromRgb(0xEF, 0x6C, 0x00)),
            RiskLevel.Low    => new SolidColorBrush(Color.FromRgb(0xF9, 0xA8, 0x25)),
            _                => new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32))
        } : Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
