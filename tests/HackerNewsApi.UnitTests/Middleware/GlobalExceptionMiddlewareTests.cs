using System.Net;
using System.Text.Json;
using FluentAssertions;
using HackerNewsApi.Api.Middleware;
using HackerNewsApi.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace HackerNewsApi.UnitTests.Middleware;

public sealed class GlobalExceptionMiddlewareTests
{
    private static GlobalExceptionMiddleware CreateMiddleware(
        RequestDelegate next)
    {
        return new GlobalExceptionMiddleware(
            next,
            NullLogger<GlobalExceptionMiddleware>.Instance);
    }

    private static DefaultHttpContext CreateContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    private static async Task<ProblemDetails?> ReadProblemAsync(
        HttpContext ctx)
    {
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer
            .DeserializeAsync<ProblemDetails>(
                ctx.Response.Body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
    }

    [Fact]
    public async Task Invoke_NoException_CallsNext()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(CreateContext());

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task
        Invoke_InvalidRequestParameter_Returns400ProblemDetails()
    {
        var middleware = CreateMiddleware(_ =>
            throw new InvalidRequestParameterException(
                "n", "Must be between 1 and 200."));

        var ctx = CreateContext();
        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(400);
        var problem = await ReadProblemAsync(ctx);
        problem!.Status.Should().Be(400);
        problem.Detail.Should()
            .Contain("Parameter 'n' is invalid");
    }

    [Fact]
    public async Task
        Invoke_BadHttpRequestException_Returns400ProblemDetails()
    {
        var middleware = CreateMiddleware(_ =>
            throw new BadHttpRequestException("Bad request"));

        var ctx = CreateContext();
        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(400);
        var problem = await ReadProblemAsync(ctx);
        problem!.Status.Should().Be(400);
    }

    [Fact]
    public async Task
        Invoke_HackerNewsApiException_Returns503ProblemDetails()
    {
        var middleware = CreateMiddleware(_ =>
            throw new HackerNewsApiException(
                "Upstream unavailable", statusCode: 503));

        var ctx = CreateContext();
        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(503);
        var problem = await ReadProblemAsync(ctx);
        problem!.Status.Should().Be(503);
    }

    [Fact]
    public async Task
        Invoke_TaskCanceledException_Returns503ProblemDetails()
    {
        var middleware = CreateMiddleware(_ =>
            throw new TaskCanceledException("Timeout"));

        var ctx = CreateContext();
        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(503);
        var problem = await ReadProblemAsync(ctx);
        problem!.Status.Should().Be(503);
    }

    [Fact]
    public async Task
        Invoke_UnhandledException_Returns500ProblemDetails()
    {
        var middleware = CreateMiddleware(_ =>
            throw new InvalidOperationException("Unexpected"));

        var ctx = CreateContext();
        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(500);
        var problem = await ReadProblemAsync(ctx);
        problem!.Status.Should().Be(500);
    }
}
