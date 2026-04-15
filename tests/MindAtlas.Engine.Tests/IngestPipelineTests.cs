using MindAtlas.Engine.Ingest;

namespace MindAtlas.Engine.Tests;

public class IngestPipelineTests
{
    [Fact]
    public void ParseAgentResponse_WithMarkers_ExtractsMultiplePages()
    {
        var response = """
            Some preamble text.
            
            ---PAGE_START---
            # Quantum Computing
            
            > Introduction to quantum computing fundamentals
            
            Tags: #physics, #cs
            
            ## Content
            
            Quantum computing uses [[Qubits]] to perform calculations.
            
            ## Related
            
            - [[Classical Computing]]
            ---PAGE_END---
            
            ---PAGE_START---
            # Qubits
            
            > Quantum bits - the basic unit of quantum information
            
            Tags: #physics
            
            ## Content
            
            A qubit can be in superposition of [[Quantum Computing]] states.
            ---PAGE_END---
            """;

        var pages = IngestPipeline.ParseAgentResponse(response);

        Assert.Equal(2, pages.Count);
        Assert.Equal("Quantum Computing", pages[0].Title);
        Assert.Equal("Introduction to quantum computing fundamentals", pages[0].Summary);
        Assert.Contains("#physics", pages[0].Tags);
        Assert.Contains("Qubits", pages[0].WikiLinks);

        Assert.Equal("Qubits", pages[1].Title);
        Assert.Contains("Quantum Computing", pages[1].WikiLinks);
    }

    [Fact]
    public void ParseAgentResponse_WithoutMarkers_FallsBackToSinglePage()
    {
        var response = """
            # FallbackPage
            
            > A page without markers
            
            Tags: #fallback
            
            ## Content
            
            This response has no PAGE_START/PAGE_END markers.
            """;

        var pages = IngestPipeline.ParseAgentResponse(response);

        Assert.Single(pages);
        Assert.Equal("FallbackPage", pages[0].Title);
        Assert.Equal("A page without markers", pages[0].Summary);
    }

    [Fact]
    public void ParseAgentResponse_EmptyResponse_ReturnsEmpty()
    {
        var pages = IngestPipeline.ParseAgentResponse("");

        Assert.Empty(pages);
    }

    [Fact]
    public void ParseAgentResponse_NoTitle_SkipsBlock()
    {
        var response = """
            ---PAGE_START---
            Some content without a title heading
            ---PAGE_END---
            """;

        var pages = IngestPipeline.ParseAgentResponse(response);

        Assert.Empty(pages);
    }

    [Fact]
    public void ParseAgentResponse_ExtractsWikiLinks()
    {
        var response = """
            ---PAGE_START---
            # TestPage
            
            > Test
            
            Content with [[Alpha]], [[Beta]], and [[Alpha]] again.
            ---PAGE_END---
            """;

        var pages = IngestPipeline.ParseAgentResponse(response);

        Assert.Single(pages);
        // Alpha should appear only once (deduplicated)
        Assert.Equal(2, pages[0].WikiLinks.Count);
        Assert.Contains("Alpha", pages[0].WikiLinks);
        Assert.Contains("Beta", pages[0].WikiLinks);
    }

    [Fact]
    public void ParseAgentResponse_ExtractsTags()
    {
        var response = """
            ---PAGE_START---
            # TagTest
            
            > Tags test
            
            Tags: #ai, #ml, #deep-learning
            
            Content here.
            ---PAGE_END---
            """;

        var pages = IngestPipeline.ParseAgentResponse(response);

        Assert.Single(pages);
        Assert.Equal(3, pages[0].Tags.Count);
        Assert.Contains("#ai", pages[0].Tags);
        Assert.Contains("#ml", pages[0].Tags);
        Assert.Contains("#deep-learning", pages[0].Tags);
    }
}
