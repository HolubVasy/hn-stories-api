using System.Diagnostics;
using HackerNewsApi.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace HackerNewsApi.Api.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(
        HttpContext context, Exception ex)
    {
        var (status, title) = MapException(ex);
        var traceId = Activity.Current?.Id
            ?? context.TraceIdentifier;

        if (status >= 500)
        {
            _logger.LogError(ex,
                "Unhandled exception. TraceId={TraceId} " +
                "Status={Status}",
                traceId, status);
        }
        else
        {
            _logger.LogWarning(ex,
                "Client error. TraceId={TraceId} " +
                "Status={Status}",
                traceId, status);
        }

        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110",
            Title = title,
            Status = status,
            Detail = ex.Message,
            Extensions = { ["traceId"] = traceId }
        };

        context.Response.StatusCode = status;
        context.Response.ContentType =
            "application/problem+json";

        await context.Response.WriteAsJsonAsync(problem);
    }

    private static (int status, string title) MapException(
        Exception ex)
    {
        return ex switch
        {
            InvalidRequestParameterException e =>
                (e.StatusCode, "Bad Request"),
            BadHttpRequestException =>
                (400, "Bad Request"),
            HackerNewsApiException e =>
                (e.StatusCode, GetTitle(e.StatusCode)),
            HttpRequestException =>
                (503, "Service Unavailable"),
            OperationCanceledException =>
                (503, "Service Unavailable"),
            _ => (500, "Internal Server Error")
        };
    }

    private static string GetTitle(int status) => status switch
    {
        400 => "Bad Request",
        503 => "Service Unavailable",
        _ => "Internal Server Error"
    };
}
