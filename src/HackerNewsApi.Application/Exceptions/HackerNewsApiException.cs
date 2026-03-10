namespace HackerNewsApi.Application.Exceptions;

public class HackerNewsApiException : Exception
{
    public int StatusCode { get; }

    public HackerNewsApiException(
        string message,
        int statusCode = 503)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HackerNewsApiException(
        string message,
        Exception inner,
        int statusCode = 503)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}
