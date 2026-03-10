using System.Diagnostics;
using HackerNewsApi.Application.Exceptions;
using HackerNewsApi.Application.Interfaces;
using HackerNewsApi.Application.Mappers;
using HackerNewsApi.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HackerNewsApi.Application.Services;

public sealed class StoryService : IStoryService
{
    private readonly IHackerNewsClient _client;
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxStoryCount;
    private readonly ILogger<StoryService> _logger;

    public StoryService(
        IHackerNewsClient client,
        SemaphoreSlim semaphore,
        IOptions<StoryServiceOptions> opts,
        ILogger<StoryService> logger)
    {
        _client = client;
        _semaphore = semaphore;
        _maxStoryCount = opts.Value.MaxStoryCount;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoryDto>> GetBestStoriesAsync(
        int count,
        int skip = 0,
        CancellationToken ct = default)
    {
        ValidateParameters(count, skip);

        var sw = Stopwatch.StartNew();
        var ids = await _client.GetBestStoryIdsAsync(ct);
        var items = await FetchAllStoriesAsync(ids, ct);

        var result = items
            .Where(i => i is not null && i.Score is not null)
            .Select(i => StoryMapper.ToDto(i!))
            .OrderByDescending(s => s.Score)
            .Skip(skip)
            .Take(count)
            .ToList();

        sw.Stop();
        _logger.LogInformation(
            "Returned {Count} stories (n={N}, skip={Skip}) " +
            "in {Elapsed}ms",
            result.Count, count, skip,
            sw.ElapsedMilliseconds);

        return result;
    }

    private void ValidateParameters(int count, int skip)
    {
        if (count < 1 || count > _maxStoryCount)
        {
            throw new InvalidRequestParameterException(
                "n",
                $"Must be between 1 and {_maxStoryCount} " +
                "(MaxStoryCount).");
        }

        if (skip < 0)
        {
            throw new InvalidRequestParameterException(
                "skip",
                "Must be greater than or equal to 0.");
        }
    }

    private async Task<HackerNewsItem?[]> FetchAllStoriesAsync(
        IReadOnlyList<int> ids,
        CancellationToken ct)
    {
        var tasks = ids.Select(id => FetchWithSemaphoreAsync(id, ct));
        return await Task.WhenAll(tasks);
    }

    private async Task<HackerNewsItem?> FetchWithSemaphoreAsync(
        int id,
        CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            return await _client.GetStoryAsync(id, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
