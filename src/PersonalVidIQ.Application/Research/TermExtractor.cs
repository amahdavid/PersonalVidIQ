using System.Globalization;
using System.Text;

namespace PersonalVidIQ.Application.Research;

/// <summary>
/// Pulls candidate keywords out of a title and tags: single words, two-word phrases from the title, and
/// short tags as whole phrases. Language-neutral tokenising (Unicode letters and digits); the stop-word
/// list is English function words and YouTube boilerplate only, extendable through Research:ExtraStopWords.
/// </summary>
public sealed class TermExtractor
{
    private static readonly string[] BuiltInStopWords =
    [
        // English function words
        "a", "an", "and", "are", "as", "at", "be", "but", "by", "can", "did", "do", "does", "for", "from", "had", "has", "have",
        "he", "her", "his", "how", "i", "if", "in", "into", "is", "it", "its", "just", "me", "my", "no", "not", "of", "on", "or",
        "our", "out", "she", "so", "than", "that", "the", "their", "them", "then", "there", "these", "they", "this", "to", "too",
        "up", "us", "was", "we", "were", "what", "when", "where", "which", "who", "why", "will", "with", "you", "your", "vs",
        "all", "about", "after", "before", "more", "most", "very", "here", "get", "got", "this", "only", "ever", "every",
        // YouTube boilerplate, not topics
        "shorts", "short", "video", "videos", "official", "full", "new", "episode", "ep", "part", "pt", "subscribe", "watch",
        "live", "stream", "trailer", "clip", "clips", "highlights", "reaction", "youtube", "fyp", "viral", "trending",
    ];

    private readonly HashSet<string> _stopWords;

    public TermExtractor(IEnumerable<string>? extraStopWords = null)
    {
        _stopWords = new HashSet<string>(BuiltInStopWords, StringComparer.Ordinal);
        foreach (var word in extraStopWords ?? [])
        {
            _stopWords.Add(Normalise(word));
        }
    }

    public IReadOnlySet<string> Extract(string title, IEnumerable<string>? tags = null)
    {
        var terms = new HashSet<string>(StringComparer.Ordinal);
        var words = Tokenise(title);

        foreach (var word in words.Where(IsKeyword))
        {
            terms.Add(word);
        }

        for (var i = 0; i + 1 < words.Count; i++)
        {
            // A phrase can't start or end with a stop word ("of the"), but may contain none either way.
            if (IsKeyword(words[i]) && IsKeyword(words[i + 1]))
            {
                terms.Add($"{words[i]} {words[i + 1]}");
            }
        }

        foreach (var tag in tags ?? [])
        {
            var tagWords = Tokenise(tag);
            if (tagWords.Count is >= 1 and <= 4 && tagWords.Any(IsKeyword))
            {
                var phrase = string.Join(' ', tagWords);
                if (!_stopWords.Contains(phrase))
                {
                    terms.Add(phrase);
                }
            }
        }

        return terms;
    }

    public static string Normalise(string text) => string.Join(' ', Tokenise(text));

    public bool IsStopWord(string term) => _stopWords.Contains(term);

    private bool IsKeyword(string word) =>
        word.Length >= 2 && !_stopWords.Contains(word) && !word.All(char.IsDigit) || IsYear(word);

    // Years ("2026") are useful topic markers; other bare numbers aren't.
    private static bool IsYear(string word) =>
        word.Length == 4 && word.All(char.IsDigit) && int.Parse(word, CultureInfo.InvariantCulture) is >= 1900 and <= 2100;

    private static List<string> Tokenise(string text)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var ch in (text ?? string.Empty).Normalize(NormalizationForm.FormKC))
        {
            if (char.IsLetterOrDigit(ch) || (ch == '\'' && current.Length > 0))
            {
                current.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                Flush();
            }
        }

        Flush();
        return words;

        void Flush()
        {
            if (current.Length > 0)
            {
                var word = current.ToString().TrimEnd('\'');
                if (word.EndsWith("'s", StringComparison.Ordinal))
                {
                    word = word[..^2];
                }

                if (word.Length > 0)
                {
                    words.Add(word);
                }

                current.Clear();
            }
        }
    }
}
