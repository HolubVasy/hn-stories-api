using FluentAssertions;
using HackerNewsApi.Application.Exceptions;
using HackerNewsApi.Application.Interfaces;
using HackerNewsApi.Application.Models;
using HackerNewsApi.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HackerNewsApi.UnitTests.Services;

public sealed class StoryServiceTests
{
    private static IOptions<StoryServiceOptions> DefaultOpts(
        int maxCount = 200)
    {
        return Options.Create(
            new StoryServiceOptions { MaxStoryCount = maxCount });
    }

    private static StoryService CreateSut(
        Mock<IHackerNewsClient> clientMock,
        SemaphoreSlim? semaphore = null,
        int maxStoryCount = 200)
    {
        return new StoryService(
            clientMock.Object,
            semaphore ?? new SemaphoreSlim(20),
            DefaultOpts(maxStoryCount),
            NullLogger<StoryService>.Instance);
    }

    private static HackerNewsItem MakeItem(int id, int score) =>
        new(id, $"Story {id}", $"https://example.com/{id}",
            "user", 1000000L, score, 10, "story");

    private static Mock<IHackerNewsClient> SetupClient(
        IReadOnlyList<int> ids,
        IReadOnlyList<HackerNewsItem?> items)
    {
        var mock = new Mock<IHackerNewsClient>();
        mock.Setup(c => c.GetBestStoryIdsAsync(default))
            .ReturnsAsync(ids);

        for (int i = 0; i < ids.Count; i++)
        {
            var id = ids[i];
            var item = items[i];
            mock.Setup(c => c.GetStoryAsync(id, default))
                .ReturnsAsync(item);
        }

        return mock;
    }

    [Fact]
    public async Task GetBestStories_ValidCount_ReturnsCorrectNumber()
    {
        var ids = new List<int> { 1, 2, 3, 4, 5 };
        var items = ids.Select(i => MakeItem(i, i * 10))
            .Cast<HackerNewsItem?>().ToList();
        var sut = CreateSut(SetupClient(ids, items));

        var result = await sut.GetBestStoriesAsync(3);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task
        GetBestStories_ReturnsSortedByScoreDescending()
    {
        var ids = new List<int> { 1, 2, 3 };
        var items = new HackerNewsItem?[]
        {
            MakeItem(1, 10),
            MakeItem(2, 50),
            MakeItem(3, 30)
        };
        var sut = CreateSut(SetupClient(ids, items));

        var result = await sut.GetBestStoriesAsync(3);

        result.Select(s => s.Score)
            .Should().ContainInOrder(50, 30, 10);
    }

    [Fact]
    public async Task
        GetBestStories_CountZero_ThrowsInvalidRequestParameter()
    {
        var sut = CreateSut(new Mock<IHackerNewsClient>());

        var act = async () =>
            await sut.GetBestStoriesAsync(0);

        await act.Should()
            .ThrowAsync<InvalidRequestParameterException>();
    }

    [Fact]
    public async Task
        GetBestStories_CountNegative_ThrowsInvalidRequestParameter()
    {
        var sut = CreateSut(new Mock<IHackerNewsClient>());

        var act = async () =>
            await sut.GetBestStoriesAsync(-1);

        await act.Should()
            .ThrowAsync<InvalidRequestParameterException>();
    }

    [Fact]
    public async Task
        GetBestStories_CountExceedsMax_ThrowsInvalidRequestParameter()
    {
        var sut = CreateSut(
            new Mock<IHackerNewsClient>(), maxStoryCount: 10);

        var act = async () =>
            await sut.GetBestStoriesAsync(11);

        await act.Should()
            .ThrowAsync<InvalidRequestParameterException>();
    }

    [Fact]
    public async Task
        GetBestStories_NullItemsSkipped_ReturnsOnlyValid()
    {
        var ids = new List<int> { 1, 2, 3 };
        var items = new HackerNewsItem?[]
        {
            MakeItem(1, 10),
            null,
            MakeItem(3, 30)
        };
        var sut = CreateSut(SetupClient(ids, items));

        var result = await sut.GetBestStoriesAsync(5);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task
        GetBestStories_NullScoreItems_AreSkipped()
    {
        var ids = new List<int> { 1, 2 };
        var items = new HackerNewsItem?[]
        {
            new(1, "T", null, "u", 0, null, 0, "story"),
            MakeItem(2, 50)
        };
        var sut = CreateSut(SetupClient(ids, items));

        var result = await sut.GetBestStoriesAsync(5);

        result.Should().HaveCount(1);
        result[0].Score.Should().Be(50);
    }

    [Fact]
    public async Task GetBestStories_EmptyIdList_ReturnsEmpty()
    {
        var mock = new Mock<IHackerNewsClient>();
        mock.Setup(c => c.GetBestStoryIdsAsync(default))
            .ReturnsAsync(new List<int>());
        var sut = CreateSut(mock);

        var result = await sut.GetBestStoriesAsync(10);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task
        GetBestStories_WhenCancelledDuringHttpCall_PropagatesAndReleasesSemaphore()
    {
        var semaphore = new SemaphoreSlim(5, 5);
        var mock = new Mock<IHackerNewsClient>();
        mock.Setup(c => c.GetBestStoryIdsAsync(default))
            .ReturnsAsync(new List<int> { 1 });
        mock.Setup(c => c.GetStoryAsync(1, default))
            .ThrowsAsync(new OperationCanceledException());

        var sut = CreateSut(mock, semaphore);

        var act = async () =>
            await sut.GetBestStoriesAsync(1);

        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        // Semaphore must be released after the exception.
        semaphore.CurrentCount.Should().Be(5);
    }

    [Fact]
    public async Task
        GetBestStories_WhenCancelledDuringWait_PropagatesWithoutAcquiring()
    {
        var semaphore = new SemaphoreSlim(0, 1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var mock = new Mock<IHackerNewsClient>();
        mock.Setup(c => c.GetBestStoryIdsAsync(cts.Token))
            .ReturnsAsync(new List<int> { 1 });

        var sut = CreateSut(mock, semaphore);

        var act = async () =>
            await sut.GetBestStoriesAsync(1, 0, cts.Token);

        await act.Should()
            .ThrowAsync<OperationCanceledException>();

        // Semaphore count unchanged — never acquired.
        semaphore.CurrentCount.Should().Be(0);
    }

    [Fact]
    public async Task GetBestStories_WithSkip_ReturnsCorrectPage()
    {
        var ids = new List<int> { 1, 2, 3, 4, 5 };
        var items = ids.Select(i => MakeItem(i, i * 10))
            .Cast<HackerNewsItem?>().ToList();
        var sut = CreateSut(SetupClient(ids, items));

        var result = await sut.GetBestStoriesAsync(2, skip: 1);

        // Scores sorted desc: 50, 40, 30, 20, 10
        // skip 1, take 2 → 40, 30
        result.Should().HaveCount(2);
        result[0].Score.Should().Be(40);
        result[1].Score.Should().Be(30);
    }

    [Fact]
    public async Task
        GetBestStories_SkipNegative_ThrowsInvalidRequestParameter()
    {
        var sut = CreateSut(new Mock<IHackerNewsClient>());

        var act = async () =>
            await sut.GetBestStoriesAsync(5, skip: -1);

        await act.Should()
            .ThrowAsync<InvalidRequestParameterException>();
    }

    [Fact]
    public async Task
        GetBestStories_SkipBeyondAvailable_ReturnsEmpty()
    {
        var ids = new List<int> { 1, 2 };
        var items = ids.Select(i => MakeItem(i, i * 10))
            .Cast<HackerNewsItem?>().ToList();
        var sut = CreateSut(SetupClient(ids, items));

        var result = await sut.GetBestStoriesAsync(5, skip: 100);

        result.Should().BeEmpty();
    }
}
