namespace Nucleus.Links;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

public record PageMetadata(string? Title, Uri PageUri, Uri? FaviconUri);

public static class PageMetadataFetcher
{
    private static readonly HttpClient Http = CreateHttpClient();

    public static async Task<PageMetadata?> GetPageMetadataAsync(string url, CancellationToken ct = default)
    {
        if (!TryNormalizeUrl(url, out var pageUri)) return null;

        using var req = new HttpRequestMessage(HttpMethod.Get, pageUri);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
        if (!resp.IsSuccessStatusCode) return new PageMetadata(null, pageUri, TryDefaultFavicon(pageUri));

        // Only proceed if content looks like HTML
        var contentType = resp.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            return new PageMetadata(null, pageUri, TryDefaultFavicon(pageUri));
        }

        var html = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(html))
            return new PageMetadata(null, pageUri, TryDefaultFavicon(pageUri));

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var baseUri = ResolveBaseUri(doc, pageUri);
        var title = ExtractTitle(doc);
        var favicon = ExtractBestIcon(doc, baseUri) ?? TryDefaultFavicon(baseUri);

        return new PageMetadata(title, baseUri, favicon);
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            // A realistic desktop browser UA helps bypass some bot protections
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36");
        return client;
    }

    private static bool TryNormalizeUrl(string input, out Uri uri)
    {
        // If no scheme, assume https
        if (Uri.TryCreate(input, UriKind.Absolute, out uri)) return true;
        if (Uri.TryCreate($"https://{input}", UriKind.Absolute, out uri)) return true;
        return false;
    }

    private static string? ExtractTitle(HtmlDocument doc)
    {
        // Priority: og:title > twitter:title > <title>
        var ogTitle = doc.DocumentNode
            .SelectSingleNode("//meta[translate(@property,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='og:title']")?
            .GetAttributeValue("content", null);
        if (!string.IsNullOrWhiteSpace(ogTitle)) return ogTitle!.Trim();

        var twitterTitle = doc.DocumentNode
            .SelectSingleNode("//meta[translate(@name,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='twitter:title']")?
            .GetAttributeValue("content", null);
        if (!string.IsNullOrWhiteSpace(twitterTitle)) return twitterTitle!.Trim();

        var title = doc.DocumentNode.SelectSingleNode("//head/title")?.InnerText;
        return string.IsNullOrWhiteSpace(title) ? null : HtmlEntity.DeEntitize(title!.Trim());
    }

    private static Uri ResolveBaseUri(HtmlDocument doc, Uri pageUri)
    {
        // Respect <base href> when present
        var baseHref = doc.DocumentNode
            .SelectSingleNode("//head/base[@href]")?
            .GetAttributeValue("href", null);
        if (string.IsNullOrWhiteSpace(baseHref)) return pageUri;
        if (Uri.TryCreate(pageUri, baseHref, out var result)) return result;
        return pageUri;
    }

    private static Uri? ExtractBestIcon(HtmlDocument doc, Uri baseUri)
    {
        // Collect all potential icon <link>s
        var candidates = doc.DocumentNode
            .SelectNodes("//link[@rel and @href]")?
            .Select(n => new
            {
                Rel = (n.GetAttributeValue("rel", string.Empty) ?? string.Empty).ToLowerInvariant(),
                Href = n.GetAttributeValue("href", string.Empty),
                Sizes = (n.GetAttributeValue("sizes", string.Empty) ?? string.Empty).ToLowerInvariant(),
                Type = n.GetAttributeValue("type", string.Empty)
            })
            .Where(x => x.Rel.Contains("icon"))
            .ToList();

        if (candidates.Count == 0) return null;

        // Score by preference: apple-touch-icon > icon > shortcut icon > mask-icon
        static int RelScore(string rel)
        {
            if (rel.Contains("apple-touch-icon")) return 4;
            if (rel.Contains("icon")) return 3;
            if (rel.Contains("shortcut icon")) return 2;
            if (rel.Contains("mask-icon")) return 1;
            return 0;
        }

        // Parse size like "32x32" and choose largest area
        static int SizeScore(string sizes)
        {
            if (string.IsNullOrWhiteSpace(sizes)) return 0;
            // sizes can be "32x32" or "16x16 32x32 48x48"
            var max = 0;
            foreach (var token in sizes.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = token.ToLowerInvariant().Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
                {
                    max = Math.Max(max, w * h);
                }
            }
            return max;
        }

        var best = candidates
            .Select(c => new { c.Rel, c.Href, c.Sizes, Score = RelScore(c.Rel) * 1_000_000 + SizeScore(c.Sizes) })
            .OrderByDescending(c => c.Score)
            .FirstOrDefault();

        if (best == null || string.IsNullOrWhiteSpace(best.Href)) return null;

        if (!Uri.TryCreate(baseUri, best.Href, out var iconUri)) return null;
        return iconUri;
    }

    private static Uri? TryDefaultFavicon(Uri baseUri)
    {
        // Default conventional location
        if (Uri.TryCreate(baseUri, "/favicon.ico", out var ico)) return ico;
        return null;
    }
}