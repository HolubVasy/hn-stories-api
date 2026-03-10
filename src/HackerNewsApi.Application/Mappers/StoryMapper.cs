using HackerNewsApi.Application.Models;

namespace HackerNewsApi.Application.Mappers;

public static class StoryMapper
{
    /// <summary>
    /// Maps a raw HN API item to the public DTO.
    /// </summary>
    public static StoryDto ToDto(HackerNewsItem item)
    {
        return new StoryDto(
            Title: item.Title ?? string.Empty,
            Uri: string.IsNullOrEmpty(item.Url)
                ? null : item.Url,
            PostedBy: item.By ?? string.Empty,
            Time: DateTimeOffset
                .FromUnixTimeSeconds(item.Time),
            Score: item.Score ?? 0,
            CommentCount: item.Descendants ?? 0);
    }
}
