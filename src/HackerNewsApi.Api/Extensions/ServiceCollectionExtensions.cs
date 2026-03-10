using HackerNewsApi.Application.Interfaces;
using HackerNewsApi.Application.Models;
using HackerNewsApi.Application.Services;
using HackerNewsApi.Infrastructure.Caching;
using HackerNewsApi.Infrastructure.Configuration;
using HackerNewsApi.Infrastructure.HttpClients;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace HackerNewsApi.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddOptions<StoryServiceOptions>()
            .BindConfiguration(
                StoryServiceOptions.SectionName);

        // SemaphoreSlim created via factory — resolved after
        // ValidateOnStart() has validated the config.
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<
                IOptions<HackerNewsApiSettings>>();
            return new SemaphoreSlim(
                opts.Value.MaxConcurrentRequests);
        });

        services.AddSingleton<IStoryService, StoryService>();
        return services;
    }

    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<HackerNewsApiSettings>()
            .Bind(configuration.GetSection(
                HackerNewsApiSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddMemoryCache();

        var resilienceBuilder = services
            .AddHttpClient("HackerNews", (sp, client) =>
            {
                var opts = sp.GetRequiredService<
                    IOptions<HackerNewsApiSettings>>();
                client.BaseAddress =
                    new Uri(opts.Value.BaseUrl);
                // Polly TotalRequestTimeout manages all timeouts.
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddStandardResilienceHandler();

        // Configure resilience options via named options pattern.
        resilienceBuilder.Services
            .AddOptions<HttpStandardResilienceOptions>(
                resilienceBuilder.PipelineName)
            .Configure<IOptions<HackerNewsApiSettings>>(
                (options, settings) =>
                {
                    var cfg = settings.Value;

                    options.TotalRequestTimeout.Timeout =
                        TimeSpan.FromSeconds(
                            cfg.TotalTimeoutSeconds);

                    options.Retry.MaxRetryAttempts =
                        cfg.RetryCount;
                    options.Retry.Delay =
                        TimeSpan.FromSeconds(
                            cfg.RetryBaseDelaySeconds);

                    options.CircuitBreaker.FailureRatio = 0.5;
                    options.CircuitBreaker.MinimumThroughput =
                        cfg.CircuitBreakerThreshold;
                    options.CircuitBreaker.BreakDuration =
                        TimeSpan.FromSeconds(
                            cfg.CircuitBreakerDurationSeconds);
                });

        services.AddSingleton<HackerNewsClient>();
        services.AddSingleton<IHackerNewsClient>(sp =>
        {
            var inner = sp
                .GetRequiredService<HackerNewsClient>();
            var cache = sp
                .GetRequiredService<IMemoryCache>();
            var opts = sp.GetRequiredService<
                IOptions<HackerNewsApiSettings>>();
            var logger = sp.GetRequiredService<
                ILogger<CachedHackerNewsClient>>();
            return new CachedHackerNewsClient(
                inner, cache, opts, logger);
        });

        return services;
    }
}
