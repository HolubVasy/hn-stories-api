using HackerNewsApi.Api.Endpoints;
using HackerNewsApi.Api.Middleware;

namespace HackerNewsApi.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseCustomMiddleware(
        this WebApplication app)
    {
        app.UseMiddleware<GlobalExceptionMiddleware>();
        return app;
    }

    public static WebApplication MapStoryEndpoints(
        this WebApplication app)
    {
        StoryEndpoints.MapStoryEndpoints(app);
        return app;
    }
}
