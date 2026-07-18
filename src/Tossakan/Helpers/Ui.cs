using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Tossakan.Services;
using Windows.UI;

namespace Tossakan.Helpers;

public static class Ui
{
    /// <summary>Trello-style board background palette.</summary>
    public static readonly string[] BoardPalette =
    {
        "#0079BF", "#D29034", "#519839", "#B04632", "#89609E",
        "#CD5A91", "#4BBF6B", "#00AECC", "#838C91",
    };

    /// <summary>Trello-style label color palette.</summary>
    public static readonly string[] LabelPalette =
    {
        "#61BD4F", "#F2D600", "#FF9F1A", "#EB5A46", "#C377E0",
        "#0079BF", "#00C2E0", "#51E898", "#FF78CB", "#344563",
    };

    public static Color ToColor(string hex)
    {
        hex = hex.TrimStart('#');
        return Color.FromArgb(
            255,
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }

    public static SolidColorBrush ToBrush(string? hex) =>
        string.IsNullOrEmpty(hex)
            ? new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))
            : new SolidColorBrush(ToColor(hex));

    /// <summary>The board's photo if set and still on disk, otherwise its solid color.</summary>
    public static Brush ToBackgroundBrush(string? imagePath, string color)
    {
        if (imagePath == BackgroundImageService.DefaultImageMarker)
            imagePath = BackgroundImageService.DefaultImagePath;
        else if (imagePath == BackgroundImageService.HomeDefaultImageMarker)
            imagePath = BackgroundImageService.DefaultHomeImagePath;

        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
        {
            return new ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(imagePath)),
                Stretch = Stretch.UniformToFill,
            };
        }
        return ToBrush(color);
    }

    /// <summary>A darker shade of the given color, for hover/pressed accents on colored surfaces.</summary>
    public static SolidColorBrush ToDarkerBrush(string hex, double factor = 0.85)
    {
        var c = ToColor(hex);
        return new SolidColorBrush(Color.FromArgb(
            255, (byte)(c.R * factor), (byte)(c.G * factor), (byte)(c.B * factor)));
    }

    public static string FormatDueDate(DateTime? date) =>
        date is null ? "" : date.Value.ToString("MMM d");

    public static Visibility VisibleIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility VisibleIfNot(bool value) => VisibleIf(!value);

    public static string FormatTimestamp(DateTime date) =>
        date.Date == DateTime.Today ? $"Today {date:HH:mm}" : date.ToString("MMM d, yyyy HH:mm");
}
