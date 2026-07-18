using Microsoft.UI.Xaml.Data;
using Tossakan.Helpers;
using Tossakan.Models;

namespace Tossakan.Converters;

public class BoardBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is Board board
            ? Ui.ToBackgroundBrush(board.BackgroundImagePath, board.BackgroundColor)
            : Ui.ToBrush(null);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
