using Nucleus.Links;

namespace Nucleus.Test.Builders;

/// <summary>
/// Fluent builder for creating test Link instances.
/// </summary>
public class LinkBuilder
{
    private string _title = "Test Link";
    private string _url = "https://example.com";
    private string _thumbnailUrl = "https://example.com/favicon.ico";

    public LinkBuilder WithUrl(string url)
    {
        _url = url;
        return this;
    }

    public LinkBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public LinkBuilder WithThumbnailUrl(string thumbnailUrl)
    {
        _thumbnailUrl = thumbnailUrl;
        return this;
    }

    /// <summary>
    /// Creates a link to GitHub.
    /// </summary>
    public LinkBuilder AsGitHub()
    {
        _url = "https://github.com";
        _title = "GitHub";
        _thumbnailUrl = "https://github.com/favicon.ico";
        return this;
    }

    /// <summary>
    /// Creates a link to a documentation site.
    /// </summary>
    public LinkBuilder AsDocumentation(string framework = "AspNetCore")
    {
        _url = $"https://docs.microsoft.com/en-us/aspnet/core";
        _title = $"{framework} Documentation";
        _thumbnailUrl = "https://docs.microsoft.com/favicon.ico";
        return this;
    }

    public Link Build()
    {
        return new Link(
            Title: _title,
            Url: _url,
            ThumbnailUrl: _thumbnailUrl
        );
    }

    /// <summary>
    /// Creates a default test link with common values.
    /// </summary>
    public static Link CreateDefault() => new LinkBuilder().Build();

    /// <summary>
    /// Creates multiple links with sequential titles.
    /// </summary>
    public static IEnumerable<Link> CreateMany(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return new LinkBuilder()
                .WithUrl($"https://example{i + 1}.com")
                .WithTitle($"Test Link {i + 1}")
                .WithThumbnailUrl($"https://example{i + 1}.com/favicon.ico")
                .Build();
        }
    }
}
