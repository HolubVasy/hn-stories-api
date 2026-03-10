using HackerNewsApi.Application.Models;

namespace HackerNewsApi.Application.Interfaces;

/// <summary>
/// Client for the Hacker News Firebase API.
/// </summary>
public interface IHackerNewsClient
{
    /// <summary>
    /// Fetches the current list of best story IDs.
    /// </summary>
    Task<IReadOnlyList<int>> GetBestStoryIdsAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Fetches details for a single story by ID.
    /// Returns null if the story does not exist.
    /// </summary>
    Task<HackerNewsItem?> GetStoryAsync(
        int id,
        CancellationToken ct = default);
}
