using MindAtlas.Engine.Query;
using Xunit;

namespace MindAtlas.Engine.Tests;

public sealed class TokenUtilTests
{
    [Fact]
    public void Tokenize_MixedKoreanEnglishDigits_ReturnsLowercasedDistinct()
    {
        var result = TokenUtil.TokenizeAndNormalize("Hello, 세계! World 2024");
        Assert.Contains("hello", result);
        Assert.Contains("world", result);
        Assert.Contains("세계", result);
        Assert.Contains("2024", result);
    }

    [Fact]
    public void Tokenize_NullOrWhitespace_ReturnsEmpty()
    {
        Assert.Empty(TokenUtil.TokenizeAndNormalize(null));
        Assert.Empty(TokenUtil.TokenizeAndNormalize("   \t\n"));
        Assert.Empty(TokenUtil.TokenizeAndNormalize("!!! ???"));
    }

    [Fact]
    public void Tokenize_ShortTokensFiltered()
    {
        // Single-char tokens (< 2 runes) should be dropped.
        var result = TokenUtil.TokenizeAndNormalize("a b cd ef 가 나다");
        Assert.DoesNotContain("a", result);
        Assert.DoesNotContain("b", result);
        Assert.DoesNotContain("가", result);
        Assert.Contains("cd", result);
        Assert.Contains("ef", result);
        Assert.Contains("나다", result);
    }

    [Fact]
    public void Jaccard_BothEmpty_ReturnsZero()
    {
        var empty = new HashSet<string>();
        Assert.Equal(0.0, TokenUtil.ComputeJaccard(empty, empty));
    }

    [Fact]
    public void Jaccard_Identical_ReturnsOne()
    {
        var a = new HashSet<string> { "x", "y", "z" };
        var b = new HashSet<string> { "x", "y", "z" };
        Assert.Equal(1.0, TokenUtil.ComputeJaccard(a, b));
    }

    [Fact]
    public void Jaccard_Disjoint_ReturnsZero()
    {
        var a = new HashSet<string> { "a", "b" };
        var b = new HashSet<string> { "c", "d" };
        Assert.Equal(0.0, TokenUtil.ComputeJaccard(a, b));
    }

    [Fact]
    public void Jaccard_PartialOverlap_ReturnsRatio()
    {
        // |A∩B|=1 (b), |A∪B|=3 (a,b,c) → 1/3
        var a = new HashSet<string> { "a", "b" };
        var b = new HashSet<string> { "b", "c" };
        Assert.Equal(1.0 / 3.0, TokenUtil.ComputeJaccard(a, b), 5);
    }
}
