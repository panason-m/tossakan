using System.Text.RegularExpressions;

namespace Tossakan.Helpers;

/// <summary>Computes Jira/GitHub-style card reference codes (e.g. "TP-36") from a board name.</summary>
public static class ReferenceCode
{
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
        { "a", "an", "the", "of", "and", "for", "to", "in", "on", "&" };

    /// <summary>Initials of the board name's significant words, e.g. "Tossakan Project" -> "TP".
    /// Single-word names fall back to that word's first two letters, e.g. "Pirun" -> "PI".</summary>
    public static string ComputePrefix(string boardName)
    {
        var words = Regex.Split(boardName, @"[\s\-_/]+")
            .Where(w => w.Length > 0 && !Stopwords.Contains(w))
            .ToList();

        if (words.Count == 0) return "XX";
        if (words.Count == 1)
        {
            var w = words[0];
            return w.Length >= 2 ? w[..2].ToUpperInvariant() : (w + "X").ToUpperInvariant();
        }

        return string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
    }

    public static string Format(string boardName, int referenceNumber)
        => $"{ComputePrefix(boardName)}-{referenceNumber}";
}
