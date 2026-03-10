using System.Net.Http.Json;
using System.Text.Json;
using HackerNewsApi.Application.Exceptions;
using HackerNewsApi.Application.Interfaces;
using HackerNewsApi.Application.Models;
using Microsoft.Extensions.Logging;

namespace HackerNewsApi.Infrastructure.HttpClients;

public sealed class HackerNewsClient : IHackerNewsClient
{
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<HackerNewsClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public HackerNewsClient(
        IHttpClientFactory factory,
        ILogger<HackerNewsClient> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<int>> GetBestStoryIdsAsync(
        CancellationToken ct = default)
    {
        var client = _factory.CreateClient("HackerNews");
        _logger.LogDebug(
            "Fetching best story IDs from upstream");

        var response = await client
            .GetAsync("beststories.json", ct);

        EnsureSuccess(response, "beststories.json");

        var ids = await response.Content
            .ReadFromJsonAsync<List<int>>(JsonOpts, ct)
            ?? [];

        _logger.LogDebug(
            "Fetched {Count} story IDs", ids.Count);
        return ids;
    }

    /// <inheritdoc/>
    public async Task<HackerNewsItem?> GetStoryAsync(
        int id,
        CancellationToken ct = default)
    {
        var client = _factory.CreateClient("HackerNews");
        var url = $"item/{id}.json";

        _logger.LogDebug(
            "Fetching story {Id} from upstream", id);

        var response = await client.GetAsync(url, ct);

        if (response.StatusCode ==
            System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureSuccess(response, url);

        return await response.Content
            .ReadFromJsonAsync<HackerNewsItem>(JsonOpts, ct);
    }

    private void EnsureSuccess(
        HttpResponseMessage response, string url)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HackerNewsApiException(
                $"Upstream HN API returned " +
                $"{(int)response.StatusCode} for {url}.");
        }
    }
}
