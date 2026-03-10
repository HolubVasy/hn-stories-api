using HackerNewsApi.Application.Interfaces;
using HackerNewsApi.Application.Models;

namespace HackerNewsApi.IntegrationTests.Fakes;

public enum FakeClientMode
{
    Normal,
    HttpRequestException,
    UnexpectedException
}

public sealed class FakeHackerNewsClient : IHackerNewsClient
{
    public FakeClientMode Mode { get; set; } = FakeClientMode.Normal;

    // IDs: 1–5; ID 3 returns null (deleted story).
    private static readonly IReadOnlyList<int> StoryIds =
        [1, 2, 3, 4, 5];

    private static readonly IReadOnlyDictionary<int, HackerNewsItem>
        Stories = new Dictionary<int, HackerNewsItem>
        {
            [1] = new(1, "Story Alpha", "https://a.com",
                "alice", 1000000L, 500, 20, "story"),
            [2] = new(2, "Story Beta", "https://b.com",
                "bob", 1000100L, 300, 15, "story"),
            // ID 3 is intentionally missing → returns null
            [4] = new(4, "Story Delta", "https://d.com",
                "dave", 1000200L, 100, 5, "story"),
            [5] = new(5, "Story Epsilon", null,
                "eve", 1000300L, 200, 10, "story")
        };

    public Task<IReadOnlyList<int>> GetBestStoryIdsAsync(
        CancellationToken ct = default)
    {
        ThrowIfConfigured();
        return Task.FromResult(StoryIds);
    }

    public Task<HackerNewsItem?> GetStoryAsync(
        int id,
        CancellationToken ct = default)
    {
        ThrowIfConfigured();
        Stories.TryGetValue(id, out var item);
        return Task.FromResult(item);
    }

    private void ThrowIfConfigured()
    {
        if (Mode == FakeClientMode.HttpRequestException)
            throw new HttpRequestException(
                "Simulated upstream failure");

        if (Mode == FakeClientMode.UnexpectedException)
            throw new InvalidOperationException(
                "Simulated unexpected error");
    }
}
