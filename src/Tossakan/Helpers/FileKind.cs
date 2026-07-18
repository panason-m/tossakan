namespace Tossakan.Helpers;

/// <summary>Classifies attachment files by extension so the UI knows what it can preview inline.</summary>
public static class FileKind
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };

    private static readonly HashSet<string> MarkdownExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".md", ".markdown" };

    public static bool IsImage(string fileName) => ImageExtensions.Contains(Path.GetExtension(fileName));

    public static bool IsMarkdown(string fileName) => MarkdownExtensions.Contains(Path.GetExtension(fileName));
}
