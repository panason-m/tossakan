using Microsoft.UI.Xaml.Data;
using Tossakan.Helpers;

namespace Tossakan.Converters;

public class MarkdownConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => MarkdownRenderer.Render(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
