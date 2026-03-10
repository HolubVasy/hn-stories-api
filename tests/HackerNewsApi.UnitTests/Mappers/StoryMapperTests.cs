using FluentAssertions;
using HackerNewsApi.Application.Mappers;
using HackerNewsApi.Application.Models;

namespace HackerNewsApi.UnitTests.Mappers;

public sealed class StoryMapperTests
{
    [Fact]
    public void ToDto_ValidItem_MapsAllFieldsCorrectly()
    {
        var item = new HackerNewsItem(
            Id: 1,
            Title: "Test Story",
            Url: "https://example.com",
            By: "user123",
            Time: 1570887781L,
            Score: 100,
            Descendants: 42,
            Type: "story");

        var dto = StoryMapper.ToDto(item);

        dto.Title.Should().Be("Test Story");
        dto.Uri.Should().Be("https://example.com");
        dto.PostedBy.Should().Be("user123");
        dto.Score.Should().Be(100);
        dto.CommentCount.Should().Be(42);
        dto.Time.Should().Be(
            DateTimeOffset.FromUnixTimeSeconds(1570887781L));
    }

    [Fact]
    public void ToDto_NullUrl_SetsUriToNull()
    {
        var item = new HackerNewsItem(
            1, "Title", null, "user", 0, 10, 0, "story");

        var dto = StoryMapper.ToDto(item);

        dto.Uri.Should().BeNull();
    }

    [Fact]
    public void ToDto_EmptyUrl_SetsUriToNull()
    {
        var item = new HackerNewsItem(
            1, "Title", "", "user", 0, 10, 0, "story");

        var dto = StoryMapper.ToDto(item);

        dto.Uri.Should().BeNull();
    }

    [Fact]
    public void ToDto_NullDescendants_SetsCommentCountToZero()
    {
        var item = new HackerNewsItem(
            1, "Title", null, "user", 0, 10, null, "story");

        var dto = StoryMapper.ToDto(item);

        dto.CommentCount.Should().Be(0);
    }

    [Fact]
    public void ToDto_TimeConversion_ReturnsDateTimeOffset()
    {
        long unixTime = 1570887781L;
        var item = new HackerNewsItem(
            1, "Title", null, "user", unixTime, 10, 0, "story");

        var dto = StoryMapper.ToDto(item);

        dto.Time.Should().Be(
            DateTimeOffset.FromUnixTimeSeconds(unixTime));
        dto.Time.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ToDto_NullBy_SetsPostedByToEmpty()
    {
        var item = new HackerNewsItem(
            1, "Title", null, null, 0, 10, 0, "story");

        var dto = StoryMapper.ToDto(item);

        dto.PostedBy.Should().Be(string.Empty);
    }
}
