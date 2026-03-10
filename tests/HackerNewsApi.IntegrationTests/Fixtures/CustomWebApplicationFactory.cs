using HackerNewsApi.Application.Interfaces;
using HackerNewsApi.IntegrationTests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace HackerNewsApi.IntegrationTests.Fixtures;

public sealed class CustomWebApplicationFactory
    : WebApplicationFactory<Program>
{
    public FakeHackerNewsClient FakeClient { get; } =
        new FakeHackerNewsClient();

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace IHackerNewsClient with the fake.
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType ==
                    typeof(IHackerNewsClient));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddSingleton<IHackerNewsClient>(
                FakeClient);

            // Fresh IMemoryCache per factory instance
            // to prevent state leaking between tests.
            var cacheDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IMemoryCache));
            if (cacheDescriptor is not null)
                services.Remove(cacheDescriptor);

            services.AddSingleton<IMemoryCache>(
                _ => new MemoryCache(
                    new MemoryCacheOptions()));
        });
    }
}
