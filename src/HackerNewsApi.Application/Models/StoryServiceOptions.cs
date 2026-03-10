namespace HackerNewsApi.Application.Models;

public sealed class StoryServiceOptions
{
    public const string SectionName = "HackerNewsApi";

    public int MaxStoryCount { get; init; } = 200;
}
