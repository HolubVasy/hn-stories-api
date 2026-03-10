using FluentAssertions;
using HackerNewsApi.Application.Interfaces;
using HackerNewsApi.Application.Models;
using HackerNewsApi.Infrastructure.Caching;
using HackerNewsApi.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HackerNewsApi.UnitTests.Caching;

public sealed class CachedHackerNewsClientTests
{
    private static IOptions<HackerNewsApiSettings> DefaultOpts(
        int idsTtlSeconds = 30, int storyTtlMinutes = 5)
    {
        return Options.Create(new HackerNewsApiSettings
        {
            BaseUrl = "https://hacker-news.firebaseio.com/v0/",
            CacheDurationMinutes = storyTtlMinutes,
            MaxConcurrentRequests = 20,
            TotalTimeoutSeconds = 30,
            MaxStoryCount = 200,
            RetryCount = 3,
            RetryBaseDelaySeconds = 2,
            CircuitBreakerThreshold = 5,
            CircuitBreakerDurationSeconds = 30,
            BestStoryIdsCacheDurationSeconds = idsTtlSeconds
        });
    }

    private static (CachedHackerNewsClient client,
        IMemoryCache cache,
        Mock<IHackerNewsClient> innerMock)
        CreateSut(IOptions<HackerNewsApiSettings>? opts = null)
    {
        var inner = new Mock<IHackerNewsClient>();
        var cache = new MemoryCache(
            new MemoryCacheOptions());
        var client = new CachedHackerNewsClient(
            inner.Object,
            cache,
            opts ?? DefaultOpts(),
            NullLogger<CachedHackerNewsClient>.Instance);

        return (client, cache, inner);
    }

    [Fact]
    public async Task GetBestStoryIds_FirstCall_CallsInnerClient()
    {
        var (sut, _, inner) = CreateSut();
        var ids = new List<int> { 1, 2, 3 };
        inner.Setup(c => c.GetBestStoryIdsAsync(default))
            .ReturnsAsync(ids);

        var result = await sut.GetBestStoryIdsAsync();

        result.Should().BeEquivalentTo(ids);
        inner.Verify(
            c => c.GetBestStoryIdsAsync(default), Times.Once);
    }

    [Fact]
    public async Task GetBestStoryIds_SecondCall_ReturnsCached()
    {
        var (sut, _, inner) = CreateSut();
        inner.Setup(c => c.GetBestStoryIdsAsync(default))
            .ReturnsAsync(new List<int> { 1, 2, 3 });

        await sut.GetBestStoryIdsAsync();
        await sut.GetBestStoryIdsAsync();

        inner.Verify(
            c => c.GetBestStoryIdsAsync(default), Times.Once);
    }

    [Fact]
    public async Task GetBestStoryIds_CacheExpired_CallsInnerAgain()
    {
        // Use a Moq-based IMemoryCache to simulate expiry.
        var inner = new Mock<IHackerNewsClient>();
        var cacheMock = new Mock<IMemoryCache>();
        var opts = DefaultOpts(idsTtlSeconds: 1);

        inner.Setup(c => c.GetBestStoryIdsAsync(default))
            .ReturnsAsync(new List<int> { 1 });

        // First call: cache miss
        object? cachedVal = null;
        cacheMock.Setup(c => c.TryGetValue(
                It.IsAny<object>(),
                out cachedVal))
            .Returns(false);

        var cacheEntry = new Mock<ICacheEntry>();
        cacheEntry.SetupAllProperties();
        cacheMock.Setup(c => c.CreateEntry(It.IsAny<object>()))
            .Returns(cacheEntry.Object);

        var sut = new CachedHackerNewsClient(
            inner.Object,
            cacheMock.Object,
            opts,
            NullLogger<CachedHackerNewsClient>.Instance);

        await sut.GetBestStoryIdsAsync();

        // Second call: still a miss (simulates expiry)
        await sut.GetBestStoryIdsAsync();

        inner.Verify(
            c => c.GetBestStoryIdsAsync(default), Times.Exactly(2));
    }

    [Fact]
    public async Task GetStory_FirstCall_CallsInnerClient()
    {
        var (sut, _, inner) = CreateSut();
        var item = new HackerNewsItem(
            1, "Title", null, "user", 0, 100, 10, "story");
        inner.Setup(c => c.GetStoryAsync(1, default))
            .ReturnsAsync(item);

        var result = await sut.GetStoryAsync(1);

        result.Should().BeEquivalentTo(item);
        inner.Verify(
            c => c.GetStoryAsync(1, default), Times.Once);
    }

    [Fact]
    public async Task GetStory_SecondCall_ReturnsCached()
    {
        var (sut, _, inner) = CreateSut();
        var item = new HackerNewsItem(
            1, "Title", null, "user", 0, 100, 10, "story");
        inner.Setup(c => c.GetStoryAsync(1, default))
            .ReturnsAsync(item);

        await sut.GetStoryAsync(1);
        await sut.GetStoryAsync(1);

        inner.Verify(
            c => c.GetStoryAsync(1, default), Times.Once);
    }

    [Fact]
    public async Task GetStory_CacheExpired_CallsInnerAgain()
    {
        var inner = new Mock<IHackerNewsClient>();
        var cacheMock = new Mock<IMemoryCache>();
        var opts = DefaultOpts(storyTtlMinutes: 1);

        var item = new HackerNewsItem(
            1, "Title", null, "user", 0, 100, 10, "story");
        inner.Setup(c => c.GetStoryAsync(1, default))
            .ReturnsAsync(item);

        // Simulate cache always missing (expired)
        object? nullOut = null;
        cacheMock.Setup(c => c.TryGetValue(
                It.IsAny<object>(), out nullOut))
            .Returns(false);

        var cacheEntry = new Mock<ICacheEntry>();
        cacheEntry.SetupAllProperties();
        cacheEntry.Setup(e => e.ExpirationTokens)
            .Returns([]);
        cacheEntry.Setup(e => e.PostEvictionCallbacks)
            .Returns([]);
        cacheMock.Setup(c => c.CreateEntry(It.IsAny<object>()))
            .Returns(cacheEntry.Object);

        var sut = new CachedHackerNewsClient(
            inner.Object,
            cacheMock.Object,
            opts,
            NullLogger<CachedHackerNewsClient>.Instance);

        await sut.GetStoryAsync(1);
        await sut.GetStoryAsync(1);

        inner.Verify(
            c => c.GetStoryAsync(1, default), Times.Exactly(2));
    }

    [Fact]
    public async Task GetStory_DifferentIds_CachesEachSeparately()
    {
        var (sut, _, inner) = CreateSut();

        inner.Setup(c => c.GetStoryAsync(1, default))
            .ReturnsAsync(new HackerNewsItem(
                1, "One", null, "u", 0, 10, 0, "story"));
        inner.Setup(c => c.GetStoryAsync(2, default))
            .ReturnsAsync(new HackerNewsItem(
                2, "Two", null, "u", 0, 20, 0, "story"));

        await sut.GetStoryAsync(1);
        await sut.GetStoryAsync(1);
        await sut.GetStoryAsync(2);
        await sut.GetStoryAsync(2);

        inner.Verify(c => c.GetStoryAsync(1, default), Times.Once);
        inner.Verify(c => c.GetStoryAsync(2, default), Times.Once);
    }

    [Fact]
    public async Task
        GetBestStoryIds_ConcurrentMiss_MayCallInnerMultipleTimes()
    {
        var inner = new Mock<IHackerNewsClient>();
        inner.Setup(c => c.GetBestStoryIdsAsync(default))
            .ReturnsAsync(new List<int> { 1, 2, 3 });

        // Use real MemoryCache so concurrent calls hit the same store.
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new CachedHackerNewsClient(
            inner.Object,
            cache,
            DefaultOpts(),
            NullLogger<CachedHackerNewsClient>.Instance);

        // Fire N concurrent calls with no warm cache.
        var tasks = Enumerable
            .Range(0, 10)
            .Select(_ => sut.GetBestStoryIdsAsync());

        await Task.WhenAll(tasks);

        // Inner may be called more than once under concurrent miss
        // (GetOrCreateAsync does not guarantee single execution).
        inner.Verify(
            c => c.GetBestStoryIdsAsync(default),
            Times.AtLeastOnce);
    }
}
