using Microsoft.UI.Xaml.Data;
using Tossakan.Helpers;

namespace Tossakan.Converters;

public class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => Ui.ToBrush(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
