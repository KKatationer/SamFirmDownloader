using System.Globalization;
using System.Windows.Data;

namespace SamFirmDownloader.Helper;

public class BooleanToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string param)
        {
            var texts = param.Split(',');
            return boolValue ? texts[1] : texts[0];
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}