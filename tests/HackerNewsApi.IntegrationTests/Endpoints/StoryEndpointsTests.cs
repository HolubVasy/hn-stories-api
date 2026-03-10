using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using HackerNewsApi.Application.Models;
using HackerNewsApi.IntegrationTests.Fakes;
using HackerNewsApi.IntegrationTests.Fixtures;

namespace HackerNewsApi.IntegrationTests.Endpoints;

public sealed class StoryEndpointsTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public StoryEndpointsTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private void ResetClientMode() =>
        _factory.FakeClient.Mode = FakeClientMode.Normal;

    // Fake data: IDs [1,2,3,4,5]; ID3 → null.
    // Valid stories: 1(score=500), 2(score=300),
    //                5(score=200), 4(score=100).
    // Sorted desc: 500, 300, 200, 100.

    [Fact]
    public async Task
        GetBestStories_ValidN_Returns200WithCorrectCount()
    {
        var response = await _client
            .GetAsync("/api/stories/best?n=3");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stories = await response.Content
            .ReadFromJsonAsync<List<StoryDto>>(JsonOpts);
        stories!.Count.Should().Be(3);
    }

    [Fact]
    public async Task
        GetBestStories_Returns_SortedByScoreDescending()
    {
        var stories = await GetStoriesAsync(4);

        stories.Select(s => s.Score)
            .Should().ContainInOrder(500, 300, 200, 100);
    }

    [Fact]
    public async Task GetBestStories_N1_ReturnsSingleStory()
    {
        var stories = await GetStoriesAsync(1);

        stories.Should().HaveCount(1);
        stories[0].Score.Should().Be(500);
    }

    [Fact]
    public async Task GetBestStories_MissingN_Returns400()
    {
        var response = await _client
            .GetAsync("/api/stories/best");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetBestStories_NZero_Returns400()
    {
        var response = await _client
            .GetAsync("/api/stories/best?n=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetBestStories_NNegative_Returns400()
    {
        var response = await _client
            .GetAsync("/api/stories/best?n=-1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetBestStories_NExceedsMax_Returns400()
    {
        var response = await _client
            .GetAsync("/api/stories/best?n=201");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task
        GetBestStories_ResponseFormat_MatchesContract()
    {
        var response = await _client
            .GetAsync("/api/stories/best?n=1");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var story = doc.RootElement[0];

        story.TryGetProperty("title", out _).Should().BeTrue();
        story.TryGetProperty("uri", out _).Should().BeTrue();
        story.TryGetProperty("postedBy", out _).Should().BeTrue();
        story.TryGetProperty("time", out _).Should().BeTrue();
        story.TryGetProperty("score", out _).Should().BeTrue();
        story.TryGetProperty("commentCount", out _)
            .Should().BeTrue();
    }

    [Fact]
    public async Task
        GetBestStories_ValidN_ReturnsJsonContentType()
    {
        var response = await _client
            .GetAsync("/api/stories/best?n=1");

        response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/json");
    }

    [Fact]
    public async Task GetBestStories_NonIntegerN_Returns400()
    {
        var response = await _client
            .GetAsync("/api/stories/best?n=abc");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetBestStories_EmptyN_Returns400()
    {
        var response = await _client
            .GetAsync("/api/stories/best?n=");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetBestStories_NonIntegerSkip_Returns400()
    {
        var response = await _client
            .GetAsync("/api/stories/best?n=5&skip=abc");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task
        GetBestStories_WithSkip_ReturnsCorrectSubset()
    {
        var stories = await GetStoriesAsync(2, skip: 1);

        // Skip 1 (score=500), take 2 → scores 300, 200
        stories.Should().HaveCount(2);
        stories[0].Score.Should().Be(300);
        stories[1].Score.Should().Be(200);
    }

    [Fact]
    public async Task
        GetBestStories_SkipBeyondAvailable_ReturnsEmpty()
    {
        var stories = await GetStoriesAsync(5, skip: 100);

        stories.Should().BeEmpty();
    }

    [Fact]
    public async Task
        GetBestStories_TimeField_IsIso8601DateTimeOffset()
    {
        var response = await _client
            .GetAsync("/api/stories/best?n=1");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var timeStr = doc.RootElement[0]
            .GetProperty("time").GetString();

        // Pattern: 2019-10-12T13:43:01+00:00
        Regex.IsMatch(
            timeStr!,
            @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\+00:00$")
            .Should().BeTrue();
    }

    [Fact]
    public async Task
        GetBestStories_NullStoriesSkipped_ReturnsOnlyValid()
    {
        // ID 3 returns null in FakeHackerNewsClient.
        // Total valid stories = 4 (IDs 1,2,4,5).
        var stories = await GetStoriesAsync(10);

        stories.Should().HaveCount(4);
        stories.Select(s => s.Score)
            .Should().NotContain(0);
    }

    [Fact]
    public async Task
        GetBestStories_UpstreamFailure_Returns503()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.FakeClient.Mode =
            FakeClientMode.HttpRequestException;
        using var client = factory.CreateClient();

        var response = await client
            .GetAsync("/api/stories/best?n=5");

        response.StatusCode.Should()
            .Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetBestStories_NegativeSkip_Returns400()
    {
        var response = await _client
            .GetAsync("/api/stories/best?n=5&skip=-1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task
        GetBestStories_400Response_ReturnsProblemDetails()
    {
        var response = await _client
            .GetAsync("/api/stories/best?n=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("type", out _).Should().BeTrue();
        root.TryGetProperty("title", out _).Should().BeTrue();
        root.TryGetProperty("status", out var statusProp)
            .Should().BeTrue();
        root.TryGetProperty("detail", out _).Should().BeTrue();

        statusProp.GetInt32().Should().Be(
            (int)response.StatusCode);
    }

    [Fact]
    public async Task
        GetBestStories_UnexpectedException_Returns500()
    {
        using var factory = new CustomWebApplicationFactory();
        factory.FakeClient.Mode =
            FakeClientMode.UnexpectedException;
        using var client = factory.CreateClient();

        var response = await client
            .GetAsync("/api/stories/best?n=5");

        response.StatusCode.Should()
            .Be(HttpStatusCode.InternalServerError);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement
            .TryGetProperty("status", out var statusProp)
            .Should().BeTrue();
        statusProp.GetInt32().Should().Be(500);
    }

    [Fact]
    public async Task
        GetBestStories_DefaultSkip_EqualsExplicitZero()
    {
        var withDefault = await _client
            .GetAsync("/api/stories/best?n=3");
        var withZero = await _client
            .GetAsync("/api/stories/best?n=3&skip=0");

        var json1 = await withDefault.Content
            .ReadAsStringAsync();
        var json2 = await withZero.Content
            .ReadAsStringAsync();

        json1.Should().Be(json2);
    }

    private async Task<List<StoryDto>> GetStoriesAsync(
        int n, int? skip = null)
    {
        var url = $"/api/stories/best?n={n}";
        if (skip.HasValue)
            url += $"&skip={skip}";

        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content
            .ReadFromJsonAsync<List<StoryDto>>(JsonOpts)
            ?? [];
    }
}
