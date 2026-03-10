using HackerNewsApi.Application.Interfaces;
using HackerNewsApi.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace HackerNewsApi.Api.Endpoints;

public static class StoryEndpoints
{
    public static void MapStoryEndpoints(
        WebApplication app)
    {
        app.MapGet("/api/stories/best", GetBestStories)
            .WithName("GetBestStories")
            .Produces<List<StoryDto>>(200)
            .ProducesProblem(400)
            .ProducesProblem(500)
            .ProducesProblem(503);
    }

    private static async Task<IResult> GetBestStories(
        [FromQuery] int n,
        IStoryService storyService,
        CancellationToken ct,
        [FromQuery] int skip = 0)
    {
        var stories = await storyService
            .GetBestStoriesAsync(n, skip, ct);
        return Results.Ok(stories);
    }
}
