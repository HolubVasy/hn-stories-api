using System.ComponentModel.DataAnnotations;

namespace HackerNewsApi.Infrastructure.Configuration;

public sealed class HackerNewsApiSettings
{
    public const string SectionName = "HackerNewsApi";

    [Required, Url]
    public required string BaseUrl { get; init; }

    [Range(1, 1440)]
    public required int CacheDurationMinutes { get; init; }

    [Range(1, 100)]
    public required int MaxConcurrentRequests { get; init; }

    [Range(1, 300)]
    public required int TotalTimeoutSeconds { get; init; }

    [Range(1, 200)]
    public required int MaxStoryCount { get; init; }

    [Range(0, 10)]
    public required int RetryCount { get; init; }

    [Range(1, 60)]
    public required int RetryBaseDelaySeconds { get; init; }

    [Range(1, 100)]
    public required int CircuitBreakerThreshold { get; init; }

    [Range(1, 600)]
    public required int CircuitBreakerDurationSeconds
        { get; init; }

    [Range(1, 3600)]
    public required int BestStoryIdsCacheDurationSeconds
        { get; init; }
}
