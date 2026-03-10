using HackerNewsApi.Application.Models;

namespace HackerNewsApi.Application.Interfaces;

/// <summary>
/// Retrieves and ranks best stories from Hacker News.
/// </summary>
public interface IStoryService
{
    /// <summary>
    /// Returns the top <paramref name="count"/> best
    /// stories sorted by score descending, skipping
    /// the first <paramref name="skip"/> results.
    /// </summary>
    Task<IReadOnlyList<StoryDto>> GetBestStoriesAsync(
        int count,
        int skip = 0,
        CancellationToken ct = default);
}
