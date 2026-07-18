using System.Text.RegularExpressions;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI.Text;

namespace Tossakan.Helpers;

/// <summary>Renders a small, deliberately-limited markdown subset (headers, bullet/numbered lists,
/// bold/italic) as a single selectable RichTextBlock, for read-only display of card descriptions.
/// Everything lives in one RichTextBlock (rather than one TextBlock per line) so selection/copy
/// can span across headers and bullets instead of stopping at each line's own element.</summary>
public static class MarkdownRenderer
{
    private static readonly Regex HeaderPattern = new(@"^(#{1,3})\s+(.*)$");
    private static readonly Regex BulletPattern = new(@"^[-*]\s+(.*)$");
    private static readonly Regex NumberedPattern = new(@"^(\d+)\.\s+(.*)$");
    private static readonly Regex ImagePattern = new(@"^!\[([^\]]*)\]\((.+)\)$");
    private static readonly Regex InlinePattern = new(@"(`[^`]+`|\*\*[^*]+\*\*|\*[^*]+\*|_[^_]+_)");

    public static UIElement Render(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TextBlock
            {
                Text = "Add a more detailed description…",
                FontStyle = FontStyle.Italic,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            };
        }

        var richText = new RichTextBlock { IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap };

        foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (line.Length == 0)
            {
                richText.Blocks.Add(new Paragraph { Margin = new Thickness(0, 0, 0, 6) });
                continue;
            }

            var imageMatch = ImagePattern.Match(line.Trim());
            if (imageMatch.Success)
            {
                var imageParagraph = new Paragraph { Margin = new Thickness(0, 0, 0, 2) };
                imageParagraph.Inlines.Add(new InlineUIContainer
                {
                    Child = BuildImageElement(imageMatch.Groups[2].Value, imageMatch.Groups[1].Value),
                });
                richText.Blocks.Add(imageParagraph);
                continue;
            }

            var headerMatch = HeaderPattern.Match(line);
            if (headerMatch.Success)
            {
                var paragraph = BuildInlineParagraph(headerMatch.Groups[2].Value);
                paragraph.FontSize = headerMatch.Groups[1].Value.Length switch { 1 => 19, 2 => 16, _ => 14 };
                paragraph.FontWeight = FontWeights.SemiBold;
                paragraph.Margin = new Thickness(0, 6, 0, 2);
                richText.Blocks.Add(paragraph);
                continue;
            }

            var bulletMatch = BulletPattern.Match(line);
            if (bulletMatch.Success)
            {
                richText.Blocks.Add(BuildListParagraph("•", bulletMatch.Groups[1].Value));
                continue;
            }

            var numberedMatch = NumberedPattern.Match(line);
            if (numberedMatch.Success)
            {
                richText.Blocks.Add(BuildListParagraph(numberedMatch.Groups[1].Value + ".", numberedMatch.Groups[2].Value));
                continue;
            }

            richText.Blocks.Add(BuildInlineParagraph(line));
        }

        return richText;
    }

    private static UIElement BuildImageElement(string path, string alt)
    {
        if (!File.Exists(path))
        {
            return new TextBlock
            {
                Text = $"[missing image: {(alt.Length > 0 ? alt : path)}]",
                FontStyle = FontStyle.Italic,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            };
        }

        return new Image
        {
            Source = new BitmapImage(new Uri(path)),
            Stretch = Stretch.Uniform,
            MaxWidth = 320,
            MaxHeight = 240,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 4),
        };
    }

    /// <summary>A bullet/numbered line as a single paragraph with a hanging indent, so the marker sits
    /// to the left and wrapped continuation lines align under the text instead of the marker — while
    /// keeping marker and text in the same paragraph so selection flows continuously across them.</summary>
    private static Paragraph BuildListParagraph(string marker, string text)
    {
        var paragraph = BuildInlineParagraph(text);
        paragraph.Inlines.Insert(0, new Run { Text = marker + "  ", FontWeight = FontWeights.Bold });
        paragraph.Margin = new Thickness(20, 0, 0, 2);
        paragraph.TextIndent = -20;
        return paragraph;
    }

    private static Paragraph BuildInlineParagraph(string text)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 2) };
        foreach (var part in InlinePattern.Split(text))
        {
            if (part.Length == 0) continue;

            if (part.Length >= 4 && part.StartsWith("**") && part.EndsWith("**"))
                paragraph.Inlines.Add(new Run { Text = part[2..^2], FontWeight = FontWeights.Bold });
            else if (part.Length >= 2 && part[0] == '`' && part[^1] == '`')
                paragraph.Inlines.Add(new Run { Text = part[1..^1], FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") });
            else if (part.Length >= 2 && (part[0] == '*' || part[0] == '_') && part[^1] == part[0])
                paragraph.Inlines.Add(new Run { Text = part[1..^1], FontStyle = FontStyle.Italic });
            else
                paragraph.Inlines.Add(new Run { Text = part });
        }
        return paragraph;
    }
}
