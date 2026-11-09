using PersonalVidIQ.Application.Research;

namespace PersonalVidIQ.UnitTests.Research;

public sealed class TermExtractorTests
{
    private readonly TermExtractor _extractor = new();

    [Fact]
    public void Extracts_words_and_two_word_phrases_without_stop_words()
    {
        var terms = _extractor.Extract("How I Built a Budget Gaming PC in 2026!");

        Assert.Contains("budget", terms);
        Assert.Contains("gaming pc", terms);
        Assert.Contains("budget gaming", terms);
        Assert.Contains("2026", terms);
        Assert.DoesNotContain("how", terms);
        Assert.DoesNotContain("a budget", terms);
        Assert.DoesNotContain("in 2026", terms);
    }

    [Fact]
    public void Ignores_youtube_boilerplate_and_bare_numbers()
    {
        var terms = _extractor.Extract("Top 10 Recipes #shorts (Official Video)");

        Assert.Contains("recipes", terms);
        Assert.DoesNotContain("shorts", terms);
        Assert.DoesNotContain("official", terms);
        Assert.DoesNotContain("10", terms);
        Assert.DoesNotContain("top 10", terms);
    }

    [Fact]
    public void Short_tags_become_phrases_long_tags_are_skipped()
    {
        var terms = _extractor.Extract("Morning routine", ["Productivity Tips", "a very long tag that goes on and on", "the"]);

        Assert.Contains("productivity tips", terms);
        Assert.DoesNotContain("a very long tag that goes on and on", terms);
        Assert.DoesNotContain("the", terms);
    }

    [Fact]
    public void Handles_possessives_case_and_non_english_letters()
    {
        var terms = _extractor.Extract("Mbappé's Café TOUR");

        Assert.Contains("mbappé", terms);
        Assert.Contains("café tour", terms);
        Assert.Contains("mbappé café", terms);
    }

    [Fact]
    public void Extra_stop_words_are_configurable()
    {
        var extractor = new TermExtractor(["Recipe"]);

        Assert.DoesNotContain("recipe", extractor.Extract("Easy pasta recipe"));
        Assert.Contains("easy pasta", extractor.Extract("Easy pasta recipe"));
    }
}
