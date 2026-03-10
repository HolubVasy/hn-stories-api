using HackerNewsApi.Application.Interfaces;
using HackerNewsApi.Application.Models;
using HackerNewsApi.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HackerNewsApi.Infrastructure.Caching;

public sealed class CachedHackerNewsClient : IHackerNewsClient
{
    private const string BestIdsKey = "best-story-ids";

    private readonly IHackerNewsClient _inner;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _idsTtl;
    private readonly TimeSpan _storyTtl;
    private readonly ILogger<CachedHackerNewsClient> _logger;

    public CachedHackerNewsClient(
        IHackerNewsClient inner,
        IMemoryCache cache,
        IOptions<HackerNewsApiSettings> opts,
        ILogger<CachedHackerNewsClient> logger)
    {
        _inner = inner;
        _cache = cache;
        _idsTtl = TimeSpan.FromSeconds(
            opts.Value.BestStoryIdsCacheDurationSeconds);
        _storyTtl = TimeSpan.FromMinutes(
            opts.Value.CacheDurationMinutes);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<int>> GetBestStoryIdsAsync(
        CancellationToken ct = default)
    {
        if (_cache.TryGetValue(
            BestIdsKey, out IReadOnlyList<int>? cached)
            && cached is not null)
        {
            _logger.LogDebug(
                "Cache {Result} for best story IDs", "hit");
            return cached;
        }

        _logger.LogDebug(
            "Cache {Result} for best story IDs", "miss");

        var ids = await _inner.GetBestStoryIdsAsync(ct);

        _cache.Set(BestIdsKey, ids,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _idsTtl
            });

        return ids;
    }

    /// <inheritdoc/>
    public async Task<HackerNewsItem?> GetStoryAsync(
        int id,
        CancellationToken ct = default)
    {
        var key = $"story-{id}";

        return await _cache.GetOrCreateAsync(key, async entry =>
        {
            _logger.LogDebug(
                "Cache {Result} for story {Id}", "miss", id);

            entry.AbsoluteExpirationRelativeToNow = _storyTtl;
            return await _inner.GetStoryAsync(id, ct);
        });
    }
}
